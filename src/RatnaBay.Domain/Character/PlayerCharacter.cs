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
        Vitals = new PlayerVitals(Inventory);
        Equipment = new PlayerEquipment(Inventory);
        Combat = new PlayerCombat(Vitals, Equipment, Skills);
        Spells = new SpellCaster(Vitals, Skills);
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
