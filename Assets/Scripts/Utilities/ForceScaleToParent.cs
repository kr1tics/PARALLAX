using UnityEngine;

public class ForceScaleToParent : MonoBehaviour
{
    [SerializeField] private Canvas _parent;
    private Rect canvasRectLastUpdate = new();
    
    [SerializeField] private RectTransform _this;

    void Setup()
    {
        Canvas.ForceUpdateCanvases();
        SetSize();
    }

    void Update()
    {
        if (_parent.pixelRect != canvasRectLastUpdate && 
            _this.rect != _parent.pixelRect)
        {
            SetSize();
        }

        canvasRectLastUpdate = _parent.pixelRect;
    }

    void SetSize()
    {
        _this.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _parent.pixelRect.width);
        _this.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _parent.pixelRect.height);
    }
}
