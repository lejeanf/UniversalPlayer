using UnityEngine;
using UnityEngine.UI;

namespace jeanf.universalplayer
{
    public class FlatViewPresenter : MonoBehaviour
    {
        public const string CanvasName = "Flat View Presenter (UniversalPlayer)";
        public const int CanvasSortingOrder = short.MinValue;

        private Camera _camera;
        private RenderTexture _target;
        private Canvas _canvas;
        private RawImage _image;

        public bool IsPresenting => _camera != null && _target != null;
        public RenderTexture Target => _target;
        public Camera PresentedCamera => _camera;

        public static Vector2Int WindowSize => new Vector2Int(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

        public void Begin(Camera camera)
        {
            if (camera == null) return;
            if (_camera != null && _camera != camera) End();
            _camera = camera;
            EnsureTarget(WindowSize);
            EnsureCanvas();
            _canvas.enabled = true;
        }

        public void End()
        {
            if (_camera != null && _camera.targetTexture == _target) _camera.targetTexture = null;
            _camera = null;
            if (_canvas != null) _canvas.enabled = false;
            ReleaseTarget();
        }

        public void ResizeToWindowIfNeeded()
        {
            if (!IsPresenting) return;
            var size = WindowSize;
            if (_target.width == size.x && _target.height == size.y) return;
            EnsureTarget(size);
        }

        private void EnsureTarget(Vector2Int size)
        {
            if (_target != null && (_target.width != size.x || _target.height != size.y)) ReleaseTarget();
            if (_target == null)
            {
                _target = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.Default)
                {
                    name = "UniversalPlayer Flat View",
                    antiAliasing = 1
                };
                _target.Create();
            }
            _camera.targetTexture = _target;
            if (_image != null) _image.texture = _target;
        }

        private void ReleaseTarget()
        {
            if (_image != null) _image.texture = null;
            if (_target == null) return;
            _target.Release();
            Destroy(_target);
            _target = null;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null)
            {
                _image.texture = _target;
                return;
            }
            var go = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = CanvasSortingOrder;

            var imageGo = new GameObject("Flat View", typeof(RectTransform), typeof(RawImage));
            imageGo.transform.SetParent(go.transform, false);
            var rect = imageGo.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _image = imageGo.GetComponent<RawImage>();
            _image.raycastTarget = false;
            _image.texture = _target;
        }

        private void OnDisable() => End();

        private void OnDestroy()
        {
            End();
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
