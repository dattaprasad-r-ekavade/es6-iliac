using System.Collections;
using UnityEngine;

/// <summary>
/// Intro: two characters converse with subtitles, then a scenic camera flyover of Iliac Bay.
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
    [SerializeField] private Transform talkStage; // near Daggerfall overlook

    [Header("Cameras")]
    [SerializeField] private Camera cinematicCamera;
    [SerializeField] private Transform[] scenicWaypoints;

    [Header("Dialogue")]
    [SerializeField] private DialogueLine[] lines;

    [Header("Timing")]
    [SerializeField] private float scenicDuration = 22f;
    [SerializeField] private float scenicHoldAtEnd = 1.5f;

    private void Reset()
    {
        lines = DefaultLines();
    }

    public static DialogueLine[] DefaultLines()
    {
        return new[]
        {
            new DialogueLine
            {
                speaker = "Liora",
                text = "Traveler… look out over the Iliac Bay. High Rock to the north, Hammerfell to the south.",
                holdSeconds = 4.2f
            },
            new DialogueLine
            {
                speaker = "Kael",
                text = "This is only a homage — a prototype world built to explore that legend.",
                holdSeconds = 3.8f
            },
            new DialogueLine
            {
                speaker = "Liora",
                text = "Daggerfall, Wayrest, Sentinel… islands of Betony, Balfiera, Cybiades. Walk them as you will.",
                holdSeconds = 4.5f
            },
            new DialogueLine
            {
                speaker = "Kael",
                text = "No fate is written here yet. Just wind, water, and the road ahead.",
                holdSeconds = 3.6f
            },
            new DialogueLine
            {
                speaker = "Liora",
                text = "Come. Let the bay show itself… then the path is yours.",
                holdSeconds = 3.4f
            }
        };
    }

    public IEnumerator Play(GameFlowController flow)
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
        bool leftTalks = speaker.IndexOf("Liora", System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (actorLeft != null)
        {
            actorLeft.localScale = leftTalks ? new Vector3(1.15f, 1.15f, 1.15f) : Vector3.one;
        }

        if (actorRight != null)
        {
            actorRight.localScale = !leftTalks ? new Vector3(1.15f, 1.15f, 1.15f) : Vector3.one;
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
            new Vector3(-2000f, 90f, 1450f),  // Daggerfall overlook
            new Vector3(-2800f, 110f, 200f),  // Betony
            new Vector3(150f, 140f, -80f),    // Balfiera / tower
            new Vector3(2200f, 120f, 1700f),  // Wayrest approach
            new Vector3(-1600f, 100f, -2000f),// Sentinel coast
            new Vector3(-2000f, 55f, 1320f)   // return toward Daggerfall spawn
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
