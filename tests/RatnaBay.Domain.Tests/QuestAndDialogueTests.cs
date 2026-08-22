using RatnaBay.Domain;

namespace RatnaBay.Domain.Tests;

public class QuestSystemTests
{
    private PlayerCharacter _player = null!;

    private static readonly QuestDefinition BanditHunt = new()
    {
        Id = "quest.bandits", Title = "Clear the Crossroads",
        InitialStageText = "Slay bandits", TargetCount = 3, TargetEnemy = "Bandit"
    };

    private static readonly QuestDefinition FindTheBay = new()
    {
        Id = "quest.bay", Title = "Find the Bay",
        InitialStageText = "Reach the coast",
        TargetLocationIds = new[] { "city_east", "city_south" }
    };

    [SetUp]
    public void Setup()
    {
        _player = PlayerCharacter.NewGame();
        _player.Quests.AddRange(new[] { BanditHunt, FindTheBay });
    }

    [Test]
    public void AddedQuestsStartActive()
    {
        Assert.That(_player.Quests.Active.Count(), Is.EqualTo(2));
    }

    [Test]
    public void AddingTheSameQuestTwiceIsIgnored()
    {
        _player.Quests.Add(BanditHunt);
        Assert.That(_player.Quests.Quests, Has.Count.EqualTo(2));
    }

    [Test]
    public void KillsAdvanceTheStageText()
    {
        _player.Quests.NotifyEnemyKilled("Bandit");
        var quest = _player.Quests.Find("quest.bandits")!;

        Assert.Multiple(() =>
        {
            Assert.That(quest.Progress, Is.EqualTo(1));
            Assert.That(quest.StageText, Does.Contain("1/3"));
            Assert.That(quest.IsCompleted, Is.False);
        });
    }

    [Test]
    public void EnoughKillsCompleteTheQuest()
    {
        for (var i = 0; i < 3; i++) _player.Quests.NotifyEnemyKilled("Bandit");
        Assert.That(_player.Quests.Find("quest.bandits")!.IsCompleted, Is.True);
    }

    [Test]
    public void KillingTheWrongThingDoesNotCount()
    {
        _player.Quests.NotifyEnemyKilled("Mudcrab");
        Assert.That(_player.Quests.Find("quest.bandits")!.Progress, Is.Zero);
    }

    [Test]
    public void EnemyNamesMatchCaseInsensitively()
    {
        _player.Quests.NotifyEnemyKilled("bandit chief");
        Assert.That(_player.Quests.Find("quest.bandits")!.Progress, Is.EqualTo(1));
    }

    [Test]
    public void ACompletedQuestStopsCountingKills()
    {
        for (var i = 0; i < 10; i++) _player.Quests.NotifyEnemyKilled("Bandit");
        Assert.That(_player.Quests.Find("quest.bandits")!.Progress, Is.EqualTo(3));
    }

    [Test]
    public void AnyNamedLocationCompletesALocationQuest()
    {
        _player.Quests.NotifyLocation("city_south");
        Assert.That(_player.Quests.Find("quest.bay")!.IsCompleted, Is.True);
    }

    [Test]
    public void AnUnrelatedLocationCompletesNothing()
    {
        _player.Quests.NotifyLocation("some_other_place");
        Assert.That(_player.Quests.Find("quest.bay")!.IsCompleted, Is.False);
    }

    [Test]
    public void CompletionPaysTheReward()
    {
        var xp = _player.Vitals.Xp;
        var gold = _player.Vitals.Gold;

        _player.Quests.NotifyLocation("city_east");

        Assert.Multiple(() =>
        {
            Assert.That(_player.Vitals.Gold, Is.GreaterThan(gold));
            Assert.That(_player.Vitals.Xp + _player.Vitals.Level * 1000, Is.GreaterThan(xp));
        });
    }

    [Test]
    public void CompletionFiresOnceEvenIfReportederTwice()
    {
        var completions = 0;
        _player.Quests.QuestCompleted += _ => completions++;

        _player.Quests.NotifyLocation("city_east");
        _player.Quests.NotifyLocation("city_south");

        Assert.That(completions, Is.EqualTo(1));
    }

    [Test]
    public void SaveAndReloadPreservesProgress()
    {
        _player.Quests.NotifyEnemyKilled("Bandit");
        _player.Quests.NotifyLocation("city_east");

        var reloaded = PlayerCharacter.NewGame();
        reloaded.Quests.AddRange(new[] { BanditHunt, FindTheBay });
        reloaded.Quests.Restore(_player.Quests.Capture());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Quests.Find("quest.bandits")!.Progress, Is.EqualTo(1));
            Assert.That(reloaded.Quests.Find("quest.bay")!.IsCompleted, Is.True);
        });
    }

    [Test]
    public void ASaveNamingAnUnknownQuestIsSkippedRatherThanThrowing()
    {
        Assert.DoesNotThrow(() => _player.Quests.Restore(new[]
        {
            new SavedQuest { Id = "quest.from_a_future_patch", IsCompleted = true }
        }));
    }
}

public class ObjectiveServiceTests
{
    private ObjectiveService _objective = null!;

    [SetUp]
    public void Setup() => _objective = new ObjectiveService();

    [Test]
    public void ThereIsNoObjectiveUntilOneIsSet()
    {
        Assert.That(_objective.HasObjective, Is.False);
    }

    [Test]
    public void SettingAnObjectiveRecordsTheDirections()
    {
        _objective.Set("Find the smith", "Follow the river east.", "anchor.smithy");

        Assert.Multiple(() =>
        {
            Assert.That(_objective.HasObjective, Is.True);
            Assert.That(_objective.Directions, Is.EqualTo("Follow the river east."));
            Assert.That(_objective.TargetAnchorId, Is.EqualTo("anchor.smithy"));
        });
    }

    [Test]
    public void WithoutATargetThereIsNoBearing()
    {
        _objective.Set("Think about it", "Somewhere.");
        Assert.That(_objective.BearingLine(new WorldPoint(0, 0, 0)), Is.Empty);
    }

    [TestCase(0f, 100f, "north")]
    [TestCase(100f, 0f, "east")]
    [TestCase(0f, -100f, "south")]
    [TestCase(-100f, 0f, "west")]
    [TestCase(100f, 100f, "north-east")]
    public void TheBearingNamesTheRightDirection(float x, float z, string expected)
    {
        _objective.Set("Go", "There.", "anchor", new WorldPoint(x, 0f, z));
        Assert.That(_objective.BearingLine(new WorldPoint(0, 0, 0)), Does.StartWith(expected));
    }

    [Test]
    public void TheBearingIsGeneratedFromWhereThePlayerActuallyIs()
    {
        _objective.Set("Go", "There.", "anchor", new WorldPoint(0f, 0f, 100f));

        var fromSouth = _objective.BearingLine(new WorldPoint(0, 0, 0));
        var fromNorth = _objective.BearingLine(new WorldPoint(0, 0, 300f));

        Assert.That(fromSouth, Is.Not.EqualTo(fromNorth),
            "a generated direction cannot go stale the way an authored one does");
    }

    [Test]
    public void StandingOnTheTargetSaysSo()
    {
        _objective.Set("Go", "There.", "anchor", new WorldPoint(0f, 0f, 5f));

        Assert.Multiple(() =>
        {
            Assert.That(_objective.BearingLine(new WorldPoint(0, 0, 0)), Is.EqualTo("You are here."));
            Assert.That(_objective.PlayerHasArrived(new WorldPoint(0, 0, 0)), Is.True);
        });
    }

    [Test]
    public void TheDistanceIsGivenInPaces()
    {
        _objective.Set("Go", "There.", "anchor", new WorldPoint(0f, 0f, 75f));
        Assert.That(_objective.BearingLine(new WorldPoint(0, 0, 0)), Does.Contain("100 paces"));
    }

    [Test]
    public void ClearingRemovesEverything()
    {
        _objective.Set("Go", "There.", "anchor", new WorldPoint(0f, 0f, 75f));
        _objective.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(_objective.HasObjective, Is.False);
            Assert.That(_objective.TargetPosition, Is.Null);
        });
    }
}

public class TopicDialogueTests
{
    private PlayerCharacter _player = null!;
    private SpeakingActor _guard = null!;

    [SetUp]
    public void Setup()
    {
        _player = PlayerCharacter.NewGame();
        _player.Dialogue.Load(new[]
        {
            new DialogueTopic
            {
                Id = "topic.ratnapur.general", Keyword = "ratnapur",
                Response = "A port city, and getting poorer."
            },
            new DialogueTopic
            {
                Id = "topic.ratnapur.guard", Keyword = "ratnapur", ActorId = "actor.guard",
                Response = "Keep your hands where I can see them."
            },
            new DialogueTopic
            {
                Id = "topic.stones.watch", Keyword = "jiva stones", FactionId = "faction.watch",
                Response = "We count every one that leaves the vault."
            },
            new DialogueTopic
            {
                Id = "topic.route.mage", Keyword = "route", Response = "You chose the tower.",
                Conditions = new[]
                {
                    new DialogueCondition { Key = "route", Value = StoryDirector.RouteMage }
                }
            },
            new DialogueTopic
            {
                Id = "topic.burden", Keyword = "burden", Response = "You have drawn deep.",
                Conditions = new[]
                {
                    new DialogueCondition
                    {
                        Key = "player.channeled", Operator = ConditionOperator.Min, Value = "5"
                    }
                }
            }
        });

        _guard = new SpeakingActor(_player.Dialogue, "actor.guard", "City Guard", "faction.watch",
            "loc.ratnapur", "ratnapur");
    }

    [Test]
    public void AKeywordYouHaveNotLearnedGetsNoAnswer()
    {
        Assert.That(_guard.Ask("jiva stones"), Is.Null,
            "learning a keyword from one person and taking it to another is the core verb");
    }

    [Test]
    public void TalkingTeachesWhatTheActorVolunteers()
    {
        _guard.Talk();
        Assert.That(_player.Dialogue.KnowsTopic("ratnapur"), Is.True);
    }

    [Test]
    public void AMoreSpecificAnswerBeatsTheGenericOne()
    {
        _guard.Talk();
        Assert.That(_guard.Ask("ratnapur"), Is.EqualTo("Keep your hands where I can see them."));
    }

    [Test]
    public void SomeoneElseGetsTheGenericAnswer()
    {
        var merchant = new SpeakingActor(_player.Dialogue, "actor.merchant", "Merchant",
            "faction.traders", "loc.ratnapur", "ratnapur");

        merchant.Talk();
        Assert.That(merchant.Ask("ratnapur"), Is.EqualTo("A port city, and getting poorer."));
    }

    [Test]
    public void FactionGatesTheAnswer()
    {
        _player.Dialogue.LearnTopic("jiva stones");
        var outsider = new SpeakingActor(_player.Dialogue, "actor.merchant", "Merchant",
            "faction.traders", "loc.ratnapur");

        Assert.Multiple(() =>
        {
            Assert.That(_guard.Ask("jiva stones"), Is.Not.Null);
            Assert.That(outsider.Ask("jiva stones"), Is.Null);
        });
    }

    [Test]
    public void AConditionGatesTheAnswerOnStoryState()
    {
        _player.Dialogue.LearnTopic("route");
        Assert.That(_guard.Ask("route"), Is.Null);

        _player.Story.SelectRoute(StoryDirector.RouteMage);
        Assert.That(_guard.Ask("route"), Is.EqualTo("You chose the tower."));
    }

    [Test]
    public void ANumericConditionComparesRatherThanMatches()
    {
        _player.Dialogue.LearnTopic("burden");
        Assert.That(_guard.Ask("burden"), Is.Null);

        _player.Story.AddChanneled(6f);

        Assert.That(_guard.Ask("burden"), Is.EqualTo("You have drawn deep."),
            "the world reacts to how deeply the player has drawn");
    }

    [Test]
    public void TheTopicMenuNeverOffersAKeywordThatProducesSilence()
    {
        _player.Dialogue.LearnTopic("route");
        _player.Dialogue.LearnTopic("burden");
        _player.Dialogue.LearnTopic("jiva stones");

        foreach (var keyword in _guard.Talk())
            Assert.That(_guard.Ask(keyword), Is.Not.Null, $"{keyword} was offered but says nothing");
    }

    [Test]
    public void AskingRecordsTheChoiceForTheStory()
    {
        _guard.Talk();
        _guard.Ask("ratnapur");

        Assert.That(_player.Story.State.DialogueChoices, Does.ContainKey("topic.topic.ratnapur.guard"));
    }

    [Test]
    public void KnownTopicsSurviveSaveAndReload()
    {
        _guard.Talk();
        _player.Dialogue.LearnTopic("burden");

        var reloaded = PlayerCharacter.NewGame();
        reloaded.Dialogue.Restore(_player.Dialogue.Capture());

        Assert.Multiple(() =>
        {
            Assert.That(reloaded.Dialogue.KnowsTopic("ratnapur"), Is.True);
            Assert.That(reloaded.Dialogue.KnowsTopic("burden"), Is.True);
        });
    }
}
