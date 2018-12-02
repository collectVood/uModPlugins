using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spin Drop", "Iv Misticos", "2.0.0")]
    [Description("Spin around dropped weapons and tools above the ground")]
    class SpinDrop : RustPlugin
    {
        
        // TODO config
        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var category = item.info.category.ToString();
            if (category == "Weapon" || category == "Tool")
            {
                var gameObject = item.GetWorldEntity().gameObject;
                var rigidBody = gameObject.GetComponent<Rigidbody>();
                rigidBody.useGravity = false;
                rigidBody.isKinematic = true;
                gameObject.transform.position = new Vector3(gameObject.transform.position.x, gameObject.transform.position.y - 1f, gameObject.transform.position.z);
                gameObject.AddComponent<SpinDropControl>();
            }
        }

        public class SpinDropControl : MonoBehaviour
        {
            public int speed = 100;

            private void Update()
            {
                gameObject.transform.Rotate(Vector3.down * Time.deltaTime * speed);
            }
        }
    }
}