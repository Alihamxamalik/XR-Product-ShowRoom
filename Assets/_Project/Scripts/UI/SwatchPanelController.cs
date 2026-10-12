using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRShowroom
{
    /// <summary>
    /// The colour panel beside the product: one row of swatches per colour slot, the finishes, the
    /// brand's preset colourways, Randomise and "See inside". Everything on it is filled in from
    /// the product definition, so a product with different slots or colours needs no new UI.
    /// </summary>
    public sealed class SwatchPanelController : MonoBehaviour
    {
        [SerializeField] private ProductController product;
        [SerializeField] private PanelFader fader;
        [SerializeField] private TMP_Text title;

        [Header("Colours")]
        [SerializeField] private SwatchRowView[] rows = new SwatchRowView[0];

        [Header("Finish")]
        [SerializeField] private GameObject finishRow;
        [SerializeField] private Button[] finishButtons = new Button[0];
        [SerializeField] private TMP_Text[] finishLabels = new TMP_Text[0];
        [SerializeField] private GameObject[] finishRings = new GameObject[0];

        [Header("Presets")]
        [SerializeField] private GameObject presetRow;
        [SerializeField] private Button[] presetButtons = new Button[0];
        [SerializeField] private TMP_Text[] presetLabels = new TMP_Text[0];
        [SerializeField] private GameObject[] presetRings = new GameObject[0];

        [Header("Actions")]
        [SerializeField] private Button randomiseButton;
        [SerializeField] private Button explodeButton;
        [SerializeField] private TMP_Text explodeLabel;
        [SerializeField] private string explodeText = "See inside";
        [SerializeField] private string reassembleText = "Put together";
        [SerializeField] private Button swapSideButton;

        [Header("Free exploration")]
        [Tooltip("Only shown once the guided part is over.")]
        [SerializeField] private GameObject freeRow;
        [SerializeField] private Button moveButton;
        [SerializeField] private Button realSizeButton;

        private bool _highlightExplode;
        private bool _bound;

        private void Awake()
        {
            randomiseButton.onClick.AddListener(HandleRandomise);
            explodeButton.onClick.AddListener(HandleExplode);
            if (swapSideButton != null)
            {
                swapSideButton.onClick.AddListener(HandleSwapSide);
            }

            if (moveButton != null)
            {
                moveButton.onClick.AddListener(HandleMove);
            }

            if (realSizeButton != null)
            {
                realSizeButton.onClick.AddListener(HandleRealSize);
            }

            if (freeRow != null)
            {
                freeRow.SetActive(false);
            }
        }

        private void OnEnable()
        {
            product.Loaded += HandleProductLoaded;
            if (product.View != null)
            {
                Bind();
            }
        }

        private void OnDisable()
        {
            product.Loaded -= HandleProductLoaded;
            Unsubscribe();
        }

        public void Show()
        {
            fader.Show();
        }

        public void Hide()
        {
            fader.Hide();
        }

        public void SetFreeMode(bool free)
        {
            if (freeRow != null)
            {
                freeRow.SetActive(free);
            }
        }

        /// <summary>Makes the "See inside" button breathe, to draw the eye to it during the explode phase.</summary>
        public void SetExplodeHighlighted(bool highlighted)
        {
            _highlightExplode = highlighted;
            if (!highlighted)
            {
                explodeButton.transform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (_highlightExplode && !product.Explode.IsOpen)
            {
                explodeButton.transform.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(Time.unscaledTime * 5f));
            }
        }

        private void HandleProductLoaded(ProductController loaded)
        {
            Bind();
        }

        // Fills the panel in from the definition. Buttons the product doesn't need are hidden.
        private void Bind()
        {
            Unsubscribe();
            ProductDefinition definition = product.Definition;
            ColorCustomizer colors = product.Colors;

            if (title != null)
            {
                title.text = definition.displayName;
            }

            for (int r = 0; r < rows.Length; r++)
            {
                SwatchRowView row = rows[r];
                bool used = r < definition.colorSlots.Length;
                row.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                ColorSlot slot = definition.colorSlots[r];
                row.Label.text = slot.name;
                for (int c = 0; c < row.Buttons.Length; c++)
                {
                    Button button = row.Buttons[c];
                    bool offered = c < slot.colors.Length;
                    button.gameObject.SetActive(offered);
                    button.onClick.RemoveAllListeners();
                    if (!offered)
                    {
                        continue;
                    }

                    row.Fills[c].color = slot.colors[c].color;
                    int slotIndex = r;
                    int colorIndex = c;
                    button.onClick.AddListener(() => HandleSwatch(slotIndex, colorIndex));
                }
            }

            bool hasFinishes = definition.finishes.Length > 1;
            if (finishRow != null)
            {
                finishRow.SetActive(hasFinishes);
            }

            for (int f = 0; f < finishButtons.Length; f++)
            {
                bool offered = hasFinishes && f < definition.finishes.Length;
                finishButtons[f].gameObject.SetActive(offered);
                finishButtons[f].onClick.RemoveAllListeners();
                if (!offered)
                {
                    continue;
                }

                finishLabels[f].text = definition.finishes[f].name;
                int finishIndex = f;
                finishButtons[f].onClick.AddListener(() => HandleFinish(finishIndex));
            }

            if (presetRow != null)
            {
                presetRow.SetActive(definition.presets.Length > 0);
            }

            for (int p = 0; p < presetButtons.Length; p++)
            {
                bool offered = p < definition.presets.Length;
                presetButtons[p].gameObject.SetActive(offered);
                presetButtons[p].onClick.RemoveAllListeners();
                if (!offered)
                {
                    continue;
                }

                presetLabels[p].text = definition.presets[p].name;
                int presetIndex = p;
                presetButtons[p].onClick.AddListener(() => HandlePreset(presetIndex));
            }

            colors.Changed += Refresh;
            product.Explode.Changed += Refresh;
            _bound = true;
            Refresh();
        }

        private void Unsubscribe()
        {
            if (!_bound)
            {
                return;
            }

            _bound = false;
            product.Colors.Changed -= Refresh;
            product.Explode.Changed -= Refresh;
        }

        private void Refresh()
        {
            ColorCustomizer colors = product.Colors;
            for (int r = 0; r < rows.Length && r < colors.SlotCount; r++)
            {
                rows[r].Select(colors.ColorIndex(r));
            }

            for (int f = 0; f < finishRings.Length; f++)
            {
                if (finishRings[f] != null)
                {
                    finishRings[f].SetActive(f == colors.FinishIndex);
                }
            }

            int preset = colors.MatchingPreset();
            for (int p = 0; p < presetRings.Length; p++)
            {
                if (presetRings[p] != null)
                {
                    presetRings[p].SetActive(p == preset);
                }
            }

            explodeLabel.text = product.Explode.IsOpen ? reassembleText : explodeText;
        }

        private void HandleSwatch(int slot, int color)
        {
            ShowroomEvents.RaiseSfx(Sfx.SwatchClick, transform.position);
            product.Colors.SetColor(slot, color);
        }

        private void HandleFinish(int finish)
        {
            ShowroomEvents.RaiseSfx(Sfx.SwatchClick, transform.position);
            product.Colors.SetFinish(finish);
        }

        private void HandlePreset(int preset)
        {
            ShowroomEvents.RaiseSfx(Sfx.SwatchClick, transform.position);
            product.Colors.ApplyPreset(preset);
        }

        private void HandleRandomise()
        {
            ShowroomEvents.RaiseSfx(Sfx.SwatchClick, transform.position);
            product.Colors.Randomise();
        }

        private void HandleExplode()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            // Ignore presses while it is still moving, so a double tap can't open and shut it at once.
            if (!product.Explode.IsMoving)
            {
                product.Explode.Toggle(ExplodeTrigger.Button);
            }
        }

        private void HandleSwapSide()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomSettings.LeftHanded = !ShowroomSettings.LeftHanded;
        }

        private void HandleMove()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomEvents.RequestManualPlacement();
        }

        private void HandleRealSize()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            product.ResetScale();
        }
    }
}
