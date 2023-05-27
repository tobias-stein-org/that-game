using UnityEngine;

namespace tg.test.camera
{

    public class DummyPlayerController : MonoBehaviour
    {
        // Update is called once per frame
        void Update()
        {
            float speed = 10.0f * Time.deltaTime;

            if(Input.GetKey(KeyCode.W)) { this.gameObject.transform.position += Vector3.up      * speed; }
            if(Input.GetKey(KeyCode.S)) { this.gameObject.transform.position += Vector3.down    * speed; }
            if(Input.GetKey(KeyCode.A)) { this.gameObject.transform.position += Vector3.left    * speed; }
            if(Input.GetKey(KeyCode.D)) { this.gameObject.transform.position += Vector3.right   * speed; }
        }
    }
}
