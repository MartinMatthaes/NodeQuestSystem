using UnityEngine;

namespace Cainos.Pixel_Art_Top_Down___Basic.Script
{
    //when object enter or exit the trigger, put it to the assigned layer and sorting layers base on the direction
    //used in the stairs objects for player to travel between layers

    public class StairsLayerTrigger : MonoBehaviour
    {
        public Direction direction;                                 //direction of the stairs
        [Space]
        public string layerUpper;
        public string sortingLayerUpper;
        [Space]
        public string layerLower;
        public string sortingLayerLower;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y || direction == Direction.West && other.transform.position.x < transform.position.x || direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerUpper, sortingLayerUpper);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (direction == Direction.South && other.transform.position.y < transform.position.y || direction == Direction.West && other.transform.position.x < transform.position.x || direction == Direction.East && other.transform.position.x > transform.position.x) SetLayerAndSortingLayer(other.gameObject, layerLower, sortingLayerLower);
        }

        private static void SetLayerAndSortingLayer( GameObject target, string layer, string sortingLayer )
        {
            target.layer = LayerMask.NameToLayer(layer);

            target.GetComponent<SpriteRenderer>().sortingLayerName = sortingLayer;
            var srs = target.GetComponentsInChildren<SpriteRenderer>();
            foreach (var sr in srs)
            {
                sr.sortingLayerName = sortingLayer;
            }
        }

        public enum Direction
        {
            North,
            South,
            West,
            East
        }    
    }
}
