using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MRShowroom
{
    /// <summary>One row of the colour panel: a slot name and its colour buttons.</summary>
    public sealed class SwatchRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Button[] buttons = new Button[0];
        [Tooltip("The coloured disc inside each button.")]
        [SerializeField] private Image[] fills = new Image[0];
        [Tooltip("The ring shown around the selected colour.")]
        [SerializeField] private GameObject[] rings = new GameObject[0];

        public TMP_Text Label => label;
        public Button[] Buttons => buttons;
        public Image[] Fills => fills;
        public GameObject[] Rings => rings;

        public void Select(int index)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] != null)
                {
                    rings[i].SetActive(i == index);
                }
            }
        }
    }
}
