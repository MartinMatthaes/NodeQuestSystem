using UnityEngine;

namespace Cainos.Pixel_Art_Top_Down___Basic.Script
{
    //animate the sprite color base on the gradient and time
    public class SpriteColorAnimation : MonoBehaviour
    {
        public Gradient gradient;
        public float time;

        private SpriteRenderer _sr;
        private float _timer;

        private void Start()
        {
            _timer = time * Random.value;
            _sr = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (!_sr) return;
            _timer += Time.deltaTime;
            if (_timer > time) _timer = 0.0f;

            _sr.color = gradient.Evaluate(_timer / time);
        }
    }
}
