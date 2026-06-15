using UnityEngine;
using DG.Tweening;

public class EmotionController : MonoBehaviour
{
    [Header("Emotions")]
    public SpriteRenderer emotionRenderer;
    public Sprite questionIcon;
    public Sprite exclamationIcon;

    private Tween currentEmotionTween;

    public void ShowQuestion()
    {
        ShowIcon(questionIcon);
    }

    public void ShowExclamation()
    {
        ShowIcon(exclamationIcon);
    }

    public void ShowIcon(Sprite icon)
    {
        if (emotionRenderer == null || icon == null) return;
        
        if (currentEmotionTween != null && currentEmotionTween.IsActive())
        {
            currentEmotionTween.Kill();
        }

        emotionRenderer.sprite = icon;
        emotionRenderer.transform.localScale = Vector3.zero;
        emotionRenderer.gameObject.SetActive(true);

        Sequence seq = DOTween.Sequence();
        seq.Append(emotionRenderer.transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack));
        seq.AppendInterval(1.0f);
        seq.Append(emotionRenderer.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack));
        seq.OnComplete(() => emotionRenderer.gameObject.SetActive(false));

        currentEmotionTween = seq;
    }
}
