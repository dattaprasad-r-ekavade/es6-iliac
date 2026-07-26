using UnityEngine;

/// <summary>
/// Lightweight one-shot SFX (Kenney CC0 clips wired by SetupGamePresentation).
/// </summary>
public class GameSfx : MonoBehaviour
{
    public static GameSfx Instance { get; private set; }

    [SerializeField] private AudioClip uiClick;
    [SerializeField] private AudioClip uiConfirm;
    [SerializeField] private AudioClip uiOpen;
    [SerializeField] private AudioClip uiError;
    [SerializeField] private AudioClip meleeSwing;
    [SerializeField] private AudioClip meleeHit;
    [SerializeField] private AudioClip magicCast;
    [SerializeField] private AudioClip pickup;
    [SerializeField] private AudioClip levelUp;

    private AudioSource _src;

    private void Awake()
    {
        Instance = this;
        _src = gameObject.GetComponent<AudioSource>();
        if (_src == null) _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 0f;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Configure(
        AudioClip click, AudioClip confirm, AudioClip open, AudioClip error,
        AudioClip swing, AudioClip hit, AudioClip magic, AudioClip loot, AudioClip level)
    {
        uiClick = click;
        uiConfirm = confirm;
        uiOpen = open;
        uiError = error;
        meleeSwing = swing;
        meleeHit = hit;
        magicCast = magic;
        pickup = loot;
        levelUp = level;
    }

    public void PlayUiClick() => Play(uiClick, 0.55f);
    public void PlayUiConfirm() => Play(uiConfirm, 0.65f);
    public void PlayUiOpen() => Play(uiOpen, 0.55f);
    public void PlayUiError() => Play(uiError, 0.6f);
    public void PlayMeleeSwing() => Play(meleeSwing, 0.7f);
    public void PlayMeleeHit() => Play(meleeHit, 0.8f);
    public void PlayMagic() => Play(magicCast, 0.7f);
    public void PlayPickup() => Play(pickup, 0.65f);
    public void PlayLevelUp() => Play(levelUp, 0.75f);

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || _src == null) return;
        _src.PlayOneShot(clip, volume);
    }
}
