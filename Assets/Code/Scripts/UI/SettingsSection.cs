using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System;

/// <summary>
/// Accordion-style settings section that can expand/collapse with smooth animation.
/// </summary>
public class SettingsSection : MonoBehaviour
{
    [Header("Section Components")]
    [SerializeField] private Button headerButton;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private Image headerIcon;
    [SerializeField] private RectTransform contentContainer;
    [SerializeField] private LayoutElement layoutElement;

    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private AnimationCurve expandCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public event Action<SettingsSection> OnSectionClicked;

    private bool isCollapsed = true;
    private float originalHeight;
    private Coroutine animationCoroutine;

    private void Awake()
    {
        if (headerButton != null)
        {
            headerButton.onClick.AddListener(OnHeaderClicked);
        }

        if (contentContainer != null)
        {
            originalHeight = contentContainer.sizeDelta.y;
        }
    }

    public bool IsCollapsed()
    {
        return isCollapsed;
    }

    public void SetCollapsed(bool collapsed)
    {
        if (isCollapsed == collapsed) return;

        isCollapsed = collapsed;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateSection(collapsed));
    }

    private IEnumerator AnimateSection(bool toCollapsed)
    {
        float startHeight = toCollapsed ? originalHeight : 0f;
        float endHeight = toCollapsed ? 0f : originalHeight;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float curveValue = expandCurve.Evaluate(progress);
            float currentHeight = Mathf.Lerp(startHeight, endHeight, curveValue);

            if (contentContainer != null)
            {
                contentContainer.gameObject.SetActive(currentHeight > 0.1f);
                var sizeDelta = contentContainer.sizeDelta;
                sizeDelta.y = currentHeight;
                contentContainer.sizeDelta = sizeDelta;
            }

            yield return null;
        }

        if (contentContainer != null)
        {
            contentContainer.gameObject.SetActive(!toCollapsed);
            var sizeDelta = contentContainer.sizeDelta;
            sizeDelta.y = endHeight;
            contentContainer.sizeDelta = sizeDelta;
        }
    }

    private void OnHeaderClicked()
    {
        OnSectionClicked?.Invoke(this);
    }

    private void OnDestroy()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        if (headerButton != null)
        {
            headerButton.onClick.RemoveListener(OnHeaderClicked);
        }
    }
}
