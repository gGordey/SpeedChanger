using Modding;
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SpeedChanger
{
    internal class ModDisplay
    {
        internal static ModDisplay Instance;

        private string DisplayText = "";
        private DateTime DisplayExpireTime = DateTime.Now;
        private TimeSpan DisplayDuration = TimeSpan.FromSeconds(6);
        private Vector2 TextSize = new(800, 500);
        private Vector2 TextPosition = new(0.22f, 0.243f);
        private Vector2 InputTextSize = new(500, 80);
        private Vector2 InputTextPosition = new(0.02f, 0.08f);

        private GameObject _canvas;
        private UnityEngine.UI.Text _text;
        private UnityEngine.UI.Text _inputText;


        public ModDisplay()
        {
            Create();
        }
        private void Create()
        {
            if (_canvas != null) return;

            // Create base canvas
            _canvas = CanvasUtil.CreateCanvas(RenderMode.ScreenSpaceOverlay, new Vector2(1920, 1080));

            CanvasGroup canvasGroup = _canvas.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            UnityEngine.Object.DontDestroyOnLoad(_canvas);

            _text = CanvasUtil.CreateTextPanel(
                _canvas, "", 24, TextAnchor.LowerLeft,
                new CanvasUtil.RectData(TextSize, Vector2.zero, TextPosition, TextPosition),
                CanvasUtil.GetFont("Perpetua")
            ).GetComponent<UnityEngine.UI.Text>();

            _inputText = CanvasUtil.CreateTextPanel(
                _canvas, "", 22, TextAnchor.LowerLeft,
                new CanvasUtil.RectData(InputTextSize, new Vector2(30, 30), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f)),
                CanvasUtil.GetFont("Perpetua")
            ).GetComponent<UnityEngine.UI.Text>();
            _inputText.color = new Color(1f, 1f, 1f, 0.9f);
            _inputText.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_canvas != null)
            {
                UnityEngine.Object.Destroy(_canvas);
            }

            _canvas = null;
            _text = null;
            _inputText = null;
        }

        public void Update()
        {
            _text.text = DisplayText;
            _canvas.SetActive(true);
        }

        public void DisplayInput(string text)
        {
            if (_inputText == null) return;
            _inputText.text = text;
            _inputText.gameObject.SetActive(true);
        }

        public void HideInput()
        {
            if (_inputText == null) return;
            _inputText.gameObject.SetActive(false);
        }

        public void Display(string text)
        {
            DisplayText = text.Trim();
            DisplayExpireTime = DateTime.Now + DisplayDuration;
            Update();
        }
    }
}
