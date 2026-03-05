using UnityEngine;
using UnityEngine.InputSystem;

namespace Cainos.Pixel_Art_Top_Down___Basic.Script
{
    public class TopDownCharacterController : MonoBehaviour
    {
        private static readonly int Direction = Animator.StringToHash("Direction");
        private static readonly int IsMoving = Animator.StringToHash("IsMoving");
        public float speed;

        private Animator _animator;
        private Rigidbody2D _rigidbody2D;

        private void Start()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _animator = GetComponent<Animator>();
        }


        private void Update()
        {
            var dir = Vector2.zero;
            if (Keyboard.current.aKey.isPressed)
            {
                dir.x = -1;
                _animator.SetInteger(Direction, 3);
            }
            else if (Keyboard.current.dKey.isPressed)
            {
                dir.x = 1;
                _animator.SetInteger(Direction, 2);
            }

            if (Keyboard.current.wKey.isPressed)
            {
                dir.y = 1;
                _animator.SetInteger(Direction, 1);
            }
            else if (Keyboard.current.sKey.isPressed)
            {
                dir.y = -1;
                _animator.SetInteger(Direction, 0);
            }

            dir.Normalize();
            _animator.SetBool(IsMoving, dir.magnitude > 0);

            _rigidbody2D.linearVelocity = speed * dir;
        }
    }
}
