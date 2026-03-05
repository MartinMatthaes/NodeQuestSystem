using System.Collections.Generic;
using UnityEngine;

//when something get into the alta, make the runes glow
namespace Cainos.Pixel_Art_Top_Down___Basic.Script
{

    public class PropsAltar : MonoBehaviour
    {
        public List<SpriteRenderer> runes;
        public float lerpSpeed;

        private Color _curColor;
        private Color _targetColor;

        private void Awake()
        {
            _targetColor = runes[0].color;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            _targetColor.a = 1.0f;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            _targetColor.a = 0.0f;
        }

        private void Update()
        {
            _curColor = Color.Lerp(_curColor, _targetColor, lerpSpeed * Time.deltaTime);

            foreach (var r in runes)
            {
                r.color = _curColor;
            }
        }
    }
}
