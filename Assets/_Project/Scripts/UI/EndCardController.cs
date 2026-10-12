using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRShowroom
{
    /// <summary>
    /// The end card: product name, the colourway the user made, the price, and Buy now / Share /
    /// Play again. With no store URL on the product (the portfolio build) Buy now shows a mock
    /// confirmation; with one, it opens the store page in the headset's browser.
    /// </summary>
    public sealed class EndCardController : MonoBehaviour
    {
        [SerializeField] private ProductController product;
        [SerializeField] private PanelFader fader;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text taglineText;
        [SerializeField] private TMP_Text colorwayText;
        [SerializeField] private TMP_Text priceText;
        [Tooltip("One chip per colour slot, showing the chosen colours.")]
        [SerializeField] private Image[] chips = new Image[0];
        [SerializeField] private Button buyButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Button againButton;
        [SerializeField] private TMP_Text toastText;
        [SerializeField] private CanvasGroup toastGroup;
        [SerializeField] private float toastTime = 2.2f;
        [SerializeField] private string boughtMessage = "Added to your bag";
        [SerializeField] private string sharedMessage = "Link copied";

        private float _toast;
        private bool _bound;
        private bool _wanted;

        private void Awake()
        {
            buyButton.onClick.AddListener(HandleBuy);
            shareButton.onClick.AddListener(HandleShare);
            againButton.onClick.AddListener(HandleAgain);
            if (toastGroup != null)
            {
                toastGroup.alpha = 0f;
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void Show()
        {
            Unsubscribe();
            product.Colors.Changed += Refresh;
            product.Explode.Changed += HandleExplodeChanged;
            _bound = true;
            _wanted = true;
            Refresh();
            HandleExplodeChanged();
        }

        public void Hide()
        {
            Unsubscribe();
            _wanted = false;
            _toast = 0f;
            fader.Hide();
        }

        private void Unsubscribe()
        {
            if (_bound)
            {
                _bound = false;
                product.Colors.Changed -= Refresh;
                product.Explode.Changed -= HandleExplodeChanged;
            }
        }

        // The card shares its side of the product with the exploded view's labels, so it steps aside
        // while the product is apart and comes back when it is whole again.
        private void HandleExplodeChanged()
        {
            if (!_wanted)
            {
                return;
            }

            if (product.Explode.IsOpen)
            {
                fader.Hide();
            }
            else
            {
                fader.Show();
            }
        }

        // Stays live after it opens: colours changed during free exploration show up on the card too.
        private void Refresh()
        {
            ProductDefinition definition = product.Definition;
            ColorCustomizer colors = product.Colors;

            nameText.text = definition.displayName;
            if (taglineText != null)
            {
                taglineText.text = definition.tagline;
            }

            colorwayText.text = colors.ColorwayName();
            priceText.text = definition.price;

            for (int i = 0; i < chips.Length; i++)
            {
                bool used = i < colors.SlotCount;
                chips[i].gameObject.SetActive(used);
                if (used)
                {
                    chips[i].color = colors.CurrentColor(i);
                }
            }
        }

        private void Update()
        {
            if (toastGroup == null)
            {
                return;
            }

            if (_toast > 0f)
            {
                _toast -= Time.unscaledDeltaTime;
            }

            float target = _toast > 0f ? 1f : 0f;
            toastGroup.alpha = Mathf.MoveTowards(toastGroup.alpha, target, 6f * Time.unscaledDeltaTime);
        }

        private void HandleBuy()
        {
            ShowroomEvents.RaiseSfx(Sfx.Confirm, transform.position);
            ShowroomEvents.RaiseCta(CallToAction.BuyNow);

            string url = product.Definition.ctaUrl;
            if (!string.IsNullOrEmpty(url))
            {
                Application.OpenURL(url);
            }
            else
            {
                Toast(boughtMessage);
            }
        }

        private void HandleShare()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomEvents.RaiseCta(CallToAction.Share);
            Toast(sharedMessage);
        }

        private void HandleAgain()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomEvents.RaiseCta(CallToAction.PlayAgain);
        }

        private void Toast(string message)
        {
            if (toastText != null)
            {
                toastText.text = message;
            }

            _toast = toastTime;
        }
    }
}
