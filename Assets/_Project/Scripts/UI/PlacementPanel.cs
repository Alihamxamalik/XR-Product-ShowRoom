using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRShowroom
{
    /// <summary>
    /// The small card shown while the product has no home yet: what the app is doing or what it
    /// needs, and the two ways out (scan the room, or just put it in front of me). It floats in
    /// front of the user and hides itself the moment a spot has been found.
    /// </summary>
    public sealed class PlacementPanel : MonoBehaviour
    {
        [SerializeField] private PanelFader fader;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private Button scanButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private float distance = 0.6f;
        [Tooltip("Card centre below eye level (m).")]
        [SerializeField] private float drop = 0.22f;
        [SerializeField] private float sharpness = 4f;

        [Header("Wording")]
        [SerializeField] private string searching = "Finding your table";
        [SerializeField] private string needPermission = "Allow room access";
        [SerializeField] private string needPermissionDetail = "So the shoe can land on your real table.";
        [SerializeField] private string pointHands = "Point and pinch";
        [SerializeField] private string pointControllers = "Point and pull trigger";
        [SerializeField] private string pointDetail = "Aim at a table or the floor.";
        [SerializeField] private string touchHands = "Touch table, then pinch";
        [SerializeField] private string touchControllers = "Touch table, pull trigger";
        [SerializeField] private string touchDetail = "The shoe will land where your hand is.";

        private Transform _head;
        private bool _placedOnce;

        private void Awake()
        {
            scanButton.onClick.AddListener(HandleScan);
            skipButton.onClick.AddListener(HandleSkip);
        }

        private void OnEnable()
        {
            ShowroomEvents.PlacementPromptChanged += Apply;
            ShowroomEvents.InputModeChanged += HandleInputMode;
            Apply(ShowroomEvents.CurrentPlacementPrompt);
        }

        private void OnDisable()
        {
            ShowroomEvents.PlacementPromptChanged -= Apply;
            ShowroomEvents.InputModeChanged -= HandleInputMode;
        }

        private void HandleInputMode(InputMode mode)
        {
            Apply(ShowroomEvents.CurrentPlacementPrompt);
        }

        private void Apply(PlacementPrompt prompt)
        {
            bool controllers = ShowroomEvents.CurrentInputMode == InputMode.Controllers;
            bool asking = prompt == PlacementPrompt.PointAtSurface || prompt == PlacementPrompt.TouchSurface;

            switch (prompt)
            {
                case PlacementPrompt.Searching:
                    message.text = searching;
                    detail.text = string.Empty;
                    break;
                case PlacementPrompt.NeedPermission:
                    message.text = needPermission;
                    detail.text = needPermissionDetail;
                    break;
                case PlacementPrompt.PointAtSurface:
                    message.text = controllers ? pointControllers : pointHands;
                    detail.text = pointDetail;
                    break;
                case PlacementPrompt.TouchSurface:
                    message.text = controllers ? touchControllers : touchHands;
                    detail.text = touchDetail;
                    break;
            }

            // Scanning the room is offered, never forced; and only where the headset can do it.
            scanButton.gameObject.SetActive(asking && ShowroomEvents.SpaceSetupAvailable);
            skipButton.gameObject.SetActive(asking);

            if (prompt == PlacementPrompt.None)
            {
                fader.Hide();
            }
            else
            {
                if (!fader.IsShown)
                {
                    _placedOnce = false;
                }

                fader.Show();
            }
        }

        private void LateUpdate()
        {
            if (!fader.IsShown)
            {
                return;
            }

            if (_head == null)
            {
                Camera main = Camera.main;
                if (main == null)
                {
                    return;
                }

                _head = main.transform;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_head.forward, Vector3.up);
            forward = forward.sqrMagnitude < 0.01f ? Vector3.forward : forward.normalized;
            Vector3 position = _head.position + forward * distance + Vector3.down * drop;
            Quaternion rotation = Quaternion.LookRotation(position - _head.position, Vector3.up);

            if (!_placedOnce)
            {
                _placedOnce = true;
                transform.SetPositionAndRotation(position, rotation);
                return;
            }

            // Trails the head lazily instead of being glued to it.
            float blend = Ease.Damp(sharpness, Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, position, blend),
                Quaternion.Slerp(transform.rotation, rotation, blend));
        }

        private void HandleScan()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomEvents.RequestSpaceSetup();
        }

        private void HandleSkip()
        {
            ShowroomEvents.RaiseSfx(Sfx.UiPress, transform.position);
            ShowroomEvents.RequestPlacementSkip();
        }
    }
}
