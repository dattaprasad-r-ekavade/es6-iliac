using System.Collections;
using UnityEngine;

/// <summary>
/// Intro: two characters converse with subtitles, then a scenic camera flyover of Ratna Bay.
/// </summary>
public class IntroCutsceneDirector : MonoBehaviour
{
    [System.Serializable]
    public class DialogueLine
    {
        public string speaker;
        [TextArea] public string text;
        public float holdSeconds = 3.2f;
    }

    [Header("Actors")]
    [SerializeField] private Transform actorLeft;
    [SerializeField] private Transform actorRight;
    [SerializeField] private Transform talkStage; // near Caldemar overlook

    [Header("Cameras")]
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private Transform[] scenicWaypoints;

    [Header("Dialogue")]
    [SerializeField] private DialogueLine[] lines;

    [Header("Timing")]
    [SerializeField] private float scenicDuration = 22f;
    [SerializeField] private float scenicHoldAtEnd = 1.5f;

    private Vector3 _actorLeftBaseScale;
    private Vector3 _actorRightBaseScale;
    private bool _actorLeftScaleCached;
    private bool _actorRightScaleCached;

    private void Reset()
    {
        lines = DefaultLines();
    }

    /// <summary>
    /// The opening of Chapter 01: the voyage in, before the wreck.
    ///
    /// This used to be a free-roam prototype tour — two characters named Liora and Kael, who
    /// exist nowhere else in the game, listing city names and saying the coast was "a prototype
    /// of one, early, unfinished". Shipping that as the first thing a player sees undercut the
    /// chapter before it started, and a playtester reported it as a cutscene they did not
    /// recognise. It was right to not recognise it.
    ///
    /// Covers B010 (the merchant deck, the Stambha in frame) and B020 (Dhruva sails on the
    /// horizon), and stops before B030 — the pulse is witnessed in play, not narrated.
    ///
    /// **The Stambha is described but never explained.** Chapter 01 must never hint that it is
    /// an alarm; that is the Chapter 06 reveal. Here it is only something old that nobody built.
    /// </summary>
    public static DialogueLine[] DefaultLines()
    {
        return new[]
        {
            new DialogueLine
            {
                speaker = "Ship's Master",
                text = "Ratna Bay, and a fair wind for once. We'll see the lamps of Ratnapur before dark.",
                holdSeconds = 4.2f
            },
            new DialogueLine
            {
                speaker = "Deckhand",
                text = "That's Meru off the bow, master. And the pillar standing on it.",
                holdSeconds = 3.8f
            },
            new DialogueLine
            {
                speaker = "Ship's Master",
                text = "The Stambha. Older than the city, older than the trade. Nobody built it, and nobody asks.",
                holdSeconds = 4.5f
            },
            new DialogueLine
            {
                speaker = "Deckhand",
                text = "Sails to starboard — Dhruva colours. Sitting still, in open water.",
                holdSeconds = 3.8f
            },
            new DialogueLine
            {
                speaker = "Ship's Master",
                text = "They watch the stone lanes. Mind your work, and they'll watch somebody else.",
                holdSeconds = 3.6f
            }
        };
    }

    public IEnumerator Play(GameFlowController flow)
    {
        try
        {
            yield return PlaySequence(flow);
        }
        finally
        {
            CleanupActors();
        }
    }

    private IEnumerator PlaySequence(GameFlowController flow)
    {
        if (lines == null || lines.Length == 0)
        {
            lines = DefaultLines();
        }

        PrepareActors();
        flow.EnableCinematicCamera(true);
        flow.SetPlayerActive(false);

        // Dialogue phase — camera on talk stage
        if (talkStage != null && cinematicCamera != null)
        {
            cinematicCamera.transform.position = talkStage.position + new Vector3(0f, 2.2f, -6.5f);
            cinematicCamera.transform.rotation = Quaternion.LookRotation(
                (talkStage.position + Vector3.up * 1.4f) - cinematicCamera.transform.position);
        }

        foreach (var line in lines)
        {
            if (flow.ShouldSkipCutscene()) yield break;

            flow.ShowSubtitle(line.speaker, line.text);
            AnimateTalkers(line.speaker);

            float hold = Mathf.Max(1.5f, line.holdSeconds);
            float t = 0f;
            while (t < hold)
            {
                if (flow.ShouldSkipCutscene()) yield break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        flow.HideSubtitle();
        if (flow.ShouldSkipCutscene()) yield break;

        // Scenic flyover (Space / Enter / Esc skips)
        yield return ScenicFlyover(flow);
        if (flow.ShouldSkipCutscene()) yield break;

        float endHold = 0f;
        while (endHold < scenicHoldAtEnd)
        {
            if (flow.ShouldSkipCutscene()) yield break;
            endHold += Time.deltaTime;
            yield return null;
        }
    }

    private void PrepareActors()
    {
        if (talkStage == null)
        {
            var stageGo = GameObject.Find("Cutscene_TalkStage");
            if (stageGo != null) talkStage = stageGo.transform;
        }

        if (actorLeft != null)
        {
            _actorLeftBaseScale = actorLeft.localScale;
            _actorLeftScaleCached = true;
        }

        if (actorRight != null)
        {
            _actorRightBaseScale = actorRight.localScale;
            _actorRightScaleCached = true;
        }

        if (talkStage != null)
        {
            if (actorLeft != null)
            {
                actorLeft.gameObject.SetActive(true);
                actorLeft.position = talkStage.position + new Vector3(-1.6f, 0f, 0f);
                actorLeft.rotation = Quaternion.LookRotation(Vector3.back);
            }

            if (actorRight != null)
            {
                actorRight.gameObject.SetActive(true);
                actorRight.position = talkStage.position + new Vector3(1.6f, 0f, 0f);
                actorRight.rotation = Quaternion.LookRotation(Vector3.back);
            }
        }
    }

    private void AnimateTalkers(string speaker)
    {
        // Whoever speaks first stands on the left. This used to match the literal string
        // "Liora", so renaming the cast silently froze one of the two talkers.
        string first = lines != null && lines.Length > 0 ? lines[0].speaker : null;
        bool leftTalks = !string.IsNullOrEmpty(first)
            && string.Equals(speaker, first, System.StringComparison.OrdinalIgnoreCase);
        if (actorLeft != null)
        {
            var baseScale = _actorLeftScaleCached ? _actorLeftBaseScale : actorLeft.localScale;
            actorLeft.localScale = baseScale * (leftTalks ? 1.15f : 1f);
        }

        if (actorRight != null)
        {
            var baseScale = _actorRightScaleCached ? _actorRightBaseScale : actorRight.localScale;
            actorRight.localScale = baseScale * (!leftTalks ? 1.15f : 1f);
        }
    }

    private void CleanupActors()
    {
        if (actorLeft != null)
        {
            if (_actorLeftScaleCached) actorLeft.localScale = _actorLeftBaseScale;
            actorLeft.gameObject.SetActive(false);
        }

        if (actorRight != null)
        {
            if (_actorRightScaleCached) actorRight.localScale = _actorRightBaseScale;
            actorRight.gameObject.SetActive(false);
        }
    }

    private IEnumerator ScenicFlyover(GameFlowController flow)
    {
        if (cinematicCamera == null)
        {
            yield break;
        }

        var points = scenicWaypoints;
        if (points == null || points.Length < 2)
        {
            points = BuildDefaultWaypoints();
        }

        float duration = scenicDuration;
        float t = 0f;
        while (t < duration)
        {
            if (flow != null && flow.ShouldSkipCutscene()) yield break;

            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            // Ease in-out
            float e = u * u * (3f - 2f * u);
            SamplePath(points, e, out var pos, out var look);
            cinematicCamera.transform.position = pos;
            cinematicCamera.transform.rotation = Quaternion.Slerp(
                cinematicCamera.transform.rotation,
                Quaternion.LookRotation(look - pos),
                Time.deltaTime * 2.5f);
            yield return null;
        }
    }

    private Transform[] BuildDefaultWaypoints()
    {
        // Create ephemeral waypoints across the bay if none authored.
        var root = new GameObject("ScenicWaypoints_Runtime").transform;
        var defs = new[]
        {
            new Vector3(-2000f, 90f, 1450f),  // Caldemar overlook
            new Vector3(-2800f, 110f, 200f),  // Tolm
            new Vector3(150f, 140f, -80f),    // Corrath / tower
            new Vector3(2200f, 120f, 1700f),  // Estmere approach
            new Vector3(-1600f, 100f, -2000f),// Qadris coast
            new Vector3(-2000f, 55f, 1320f)   // return toward Caldemar spawn
        };

        var arr = new Transform[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var p = new GameObject($"WP_{i}").transform;
            p.SetParent(root, false);
            p.position = defs[i];
            arr[i] = p;
        }

        scenicWaypoints = arr;
        return arr;
    }

    private static void SamplePath(Transform[] points, float u, out Vector3 pos, out Vector3 lookTarget)
    {
        float f = u * (points.Length - 1);
        int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, points.Length - 2);
        float local = f - i;
        pos = Vector3.Lerp(points[i].position, points[i + 1].position, local);
        // Look slightly ahead along path, and down toward water/land.
        var ahead = Vector3.Lerp(points[i].position, points[i + 1].position, Mathf.Min(1f, local + 0.15f));
        lookTarget = ahead + Vector3.down * 25f;
    }
}
