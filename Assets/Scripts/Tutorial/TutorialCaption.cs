using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// The one line of text a tutorial step shows - in the first tutorial it is the
/// "Drag to match 2 same blocks" caption that sits under the board, exactly like
/// the reference video.
///
/// Deliberately dumb: it fades text in and out and nothing else. WHERE it sits
/// is authored on the TutorialOverlay prefab, because that is a layout decision
/// per game, not per tutorial.
/// </summary>
[DisallowMultipleComponent]
public class TutorialCaption : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private CanvasGroup group;

    [Tooltip("Fade in/out time, unscaled seconds.")]
    [SerializeField] private float fadeTime = 0.25f;

    private Coroutine _fade;
    private string _current = "";

    private void Awake()
    {
        if (group) group.alpha = 0f;
        if (label) label.text = "";
    }

    /// <summary>
    /// Shows text. Re-showing the SAME text does nothing, so a looping step can
    /// call this every cycle without restarting the fade.
    /// </summary>
    public void Show(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        if (text == _current && group && group.alpha > 0.99f) return;

        _current = text;
        if (label) label.text = text;

        StartFade(1f);
    }

    public void Hide()
    {
        _current = "";
        StartFade(0f);
    }

    private void StartFade(float target)
    {
        if (!group) return;

        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeTo(target));
    }

    private IEnumerator FadeTo(float target)
    {
        float from = group.alpha;
        float e = 0f;

        while (e < fadeTime)
        {
            e += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, fadeTime <= 0f ? 1f : Mathf.Clamp01(e / fadeTime));
            yield return null;
        }

        group.alpha = target;
        _fade = null;
    }
}
