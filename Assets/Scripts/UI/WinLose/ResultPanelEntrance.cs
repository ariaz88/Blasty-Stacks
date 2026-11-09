using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Staggered entrance shared by the win and lose screens.</summary>
public sealed class ResultPanelEntrance : MonoBehaviour
{
    private sealed class Part
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 position;
        public Vector3 scale;
        public float alpha;
    }

    private readonly Dictionary<RectTransform, Part> parts = new Dictionary<RectTransform, Part>();
    private CanvasGroup rootGroup;

    public void PlayWin(CanvasGroup panelGroup)
    {
        Begin(panelGroup);
        var sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            sequence.Insert(0f, panelGroup.DOFade(1f, 0.22f));
        }

        // The title arrives first, then the reward card and action buttons.
        Add(sequence, transform.Find("Top Ribon Holder"), 0f, new Vector2(0f, 180f), 0.78f, 0.46f);
        var rewards = transform.Find("Rewards");
        Add(sequence, rewards, 0.12f, new Vector2(0f, -75f), 0.9f, 0.48f);
        AddRewardItems(sequence, rewards, 0.28f);
        Add(sequence, transform.Find("ClaimButton"), 0.37f, new Vector2(0f, -45f), 0.8f, 0.34f);
        Add(sequence, transform.Find("ClaimButton ADDS"), 0.45f, new Vector2(0f, -45f), 0.8f, 0.34f);
        Add(sequence, transform.Find("No Thanks Button"), 0.53f, new Vector2(0f, -35f), 0.85f, 0.32f);

        Finish(sequence);
    }

    public void PlayLose(CanvasGroup panelGroup)
    {
        Begin(panelGroup);
        var sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        Add(sequence, transform.Find("Top Ribon Holder"), 0f, new Vector2(0f, 180f), 0.78f, 0.46f);
        var rewards = transform.Find("RewardsLost");
        Add(sequence, rewards, 0.12f, new Vector2(0f, -75f), 0.9f, 0.48f);
        AddRewardItems(sequence, rewards, 0.28f);
        Add(sequence, transform.Find("Replay Button"), 0.4f, new Vector2(0f, -35f), 0.8f, 0.34f);
        Add(sequence, transform.Find("No Thanks Button"), 0.49f, new Vector2(0f, -35f), 0.8f, 0.34f);

        Finish(sequence);
    }

    private void Begin(CanvasGroup panelGroup)
    {
        DOTween.Kill(this);
        Restore();
        rootGroup = panelGroup;
        if (rootGroup == null) return;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
    }

    private void AddRewardItems(Sequence sequence, Transform rewards, float firstDelay)
    {
        if (rewards == null) return;
        var background = rewards.Find("UI_LVL-Complete_Reward-BG'");
        if (background == null) return;
        Add(sequence, background.Find("CoinHolder"), firstDelay, Vector2.zero, 0.7f, 0.3f);
        Add(sequence, background.Find("GemHolder"), firstDelay + 0.07f, Vector2.zero, 0.7f, 0.3f);
        Add(sequence, background.Find("XPHolder"), firstDelay + 0.14f, Vector2.zero, 0.7f, 0.3f);
    }

    private void Add(Sequence sequence, Transform target, float delay, Vector2 offset, float startScale, float duration)
    {
        var rect = target as RectTransform;
        if (rect == null || !rect.gameObject.activeSelf) return;

        if (!parts.TryGetValue(rect, out var part))
        {
            var group = rect.GetComponent<CanvasGroup>();
            if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();
            part = new Part
            {
                rect = rect,
                group = group,
                position = rect.anchoredPosition,
                scale = rect.localScale,
                alpha = group.alpha
            };
            parts.Add(rect, part);
        }

        rect.anchoredPosition = part.position + offset;
        rect.localScale = part.scale * startScale;
        part.group.alpha = 0f;

        sequence.Insert(delay, rect.DOAnchorPos(part.position, duration).SetEase(Ease.OutCubic));
        sequence.Insert(delay, rect.DOScale(part.scale, duration).SetEase(Ease.OutBack));
        sequence.Insert(delay, part.group.DOFade(part.alpha, duration * 0.7f));
    }

    private void Finish(Sequence sequence)
    {
        sequence.OnComplete(() =>
        {
            if (rootGroup == null) return;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        });
    }

    private void Restore()
    {
        foreach (var part in parts.Values)
        {
            if (part.rect == null) continue;
            part.rect.anchoredPosition = part.position;
            part.rect.localScale = part.scale;
            if (part.group != null) part.group.alpha = part.alpha;
        }
    }

    private void OnDisable()
    {
        DOTween.Kill(this);
        Restore();
    }
}
