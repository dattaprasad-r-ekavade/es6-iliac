namespace RatnaBay.Domain;

/// <summary>
/// Everything that makes up one player, wired together.
///
/// This is what replaced the web of `Instance` singletons: the systems still know about each
/// other, but the wiring is explicit and a test can stand up a whole character in one line
/// without a scene, a game loop, or a window.
/// </summary>
public sealed class PlayerCharacter
{
    public PlayerCharacter(Inventory? inventory = null)
    {
        Inventory = inventory ?? new Inventory();
        Skills = new SkillProgression();
        LifePath = new LifePath();
        Vitals = new PlayerVitals(Inventory);
        Equipment = new PlayerEquipment(Inventory);
        Combat = new PlayerCombat(Vitals, Equipment, Skills, LifePath);
        Spells = new SpellCaster(Vitals, Skills, LifePath);
        Detection = new Detection(Skills);
        Story = new StoryDirector();
        Dialogue = new TopicDialogueService(Story);
        Quests = new QuestSystem(Vitals);
        Objective = new ObjectiveService();
        World = new WorldState();

        // Every crystal drawn is a fact the world reacts to through the `player.channeled`
        // dialogue condition, so the story state has to hear about it.
        Vitals.CrystalDrawn += () => Story.AddChanneled(1f);

        // Dialogue conditions read story flags. Recording completion here means every quest
        // can receive an authored post-completion response without UI-specific quest checks.
        Quests.QuestCompleted += quest =>
            Story.SetFlag(QuestCompletedFlag(quest.Id));
    }

    public Inventory Inventory { get; }
    public SkillProgression Skills { get; }

    /// <summary>Which of the three paths this character walks, and what it is worth.</summary>
    public LifePath LifePath { get; }
    public PlayerVitals Vitals { get; }
    public PlayerEquipment Equipment { get; }
    public PlayerCombat Combat { get; }
    public SpellCaster Spells { get; }
    public Detection Detection { get; }
    public StoryDirector Story { get; }
    public TopicDialogueService Dialogue { get; }
    public QuestSystem Quests { get; }
    public ObjectiveService Objective { get; }
    public WorldState World { get; }

    /// <summary>The line of Deepankars this save has spent, and the last body left below.</summary>
    public Legacy Legacy { get; } = new();

    public static string QuestCompletedFlag(string questId) =>
        $"flag.quest.completed.{questId}";

    /// <summary>
    /// Older saves can contain completed quests from before completion flags existed. Run
    /// after quest restore so their dialogue catches up without paying rewards a second time.
    /// </summary>
    public void ReconcileQuestStoryFlags()
    {
        foreach (var quest in Quests.Quests)
            if (quest.IsCompleted) Story.SetFlag(QuestCompletedFlag(quest.Id));
    }

    /// <summary>
    /// Commit to a life path. One entry point, because the choice touches three systems at
    /// once — the story remembers it, the skills are granted from it, and what a weapon or a
    /// spell or a price is worth all follow from it.
    /// </summary>
    public bool SelectLifePath(string? routeId)
    {
        var accepted = Story.SelectRoute(routeId);
        Skills.GrantRouteSkills(Story.State.RouteId);
        LifePath.Select(Story.State.RouteId);
        return accepted;
    }

    /// <summary>A fresh character with the starting kit, equipped.</summary>
    public static PlayerCharacter NewGame()
    {
        var player = new PlayerCharacter(Inventory.CreateStartingKit());
        // Equip what the player is already carrying, so a fresh character is not
        // inexplicably swinging bare hands past the sword in their pack.
        player.Equipment.AutoEquipBest();
        return player;
    }

    /// <summary>
    /// Advance every time-based system by one frame. The caller owns the clock, so a paused
    /// game simply stops calling this and a test can advance thirty seconds instantly.
    /// </summary>
    public void Tick(float deltaSeconds)
    {
        if (deltaSeconds <= 0f) return;

        Vitals.Tick(deltaSeconds, Combat.InCombat);
        Combat.Tick(deltaSeconds);
        Spells.Tick(deltaSeconds);
        Detection.Tick(deltaSeconds);
    }

    /// <summary>
    /// An enemy died: pay the reward, advance kill quests, and remember it stayed dead.
    /// </summary>
    public void NotifyEnemyKilled(Enemy enemy)
    {
        Vitals.AddXp(enemy.Archetype.XpReward);
        Quests.NotifyEnemyKilled(enemy.DisplayName);
        World.MarkKilled(enemy.SpawnId);
    }
}
