using UnityEngine;
using DG.Tweening;

public class EffectVfxManager : MonoBehaviour
{
    [Header("Effects")]
    public Transform stunVfx;

    private Tween stunTween;

    private void Start()
    {
        if (stunVfx != null)
        {
            stunVfx.localScale = Vector3.zero;
            stunVfx.gameObject.SetActive(false);
        }
    }

    public void ShowStun(bool show)
    {
        if (stunVfx == null) return;

        // Kill any ongoing animation
        if (stunTween != null && stunTween.IsActive())
        {
            stunTween.Kill();
        }

        if (show)
        {
            // Pop up
            stunVfx.gameObject.SetActive(true);
            stunVfx.localScale = Vector3.zero;
            stunTween = stunVfx.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }
        else
        {
            // Scale down and hide
            stunTween = stunVfx.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack)
                .OnComplete(() => stunVfx.gameObject.SetActive(false));
        }
    }
}
