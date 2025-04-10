using UnityEngine;

public class PlayerController : MonoBehaviour
{
    #region ïœêî

    [SerializeField] float PlayerSpeed;

    #endregion 

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var speed = Vector3.zero;
        if (Input.GetKey(KeyCode.W))
        {
            speed.z = PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.S))
        {
            speed.z = -PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.A))
        {
            speed.z = -PlayerSpeed;
        }
        if (Input.GetKey(KeyCode.D))
        {
            speed.z = PlayerSpeed;
        }

        transform.Translate(speed);

        //if(Input.GetKey(KeyCode.W))
        //{
        //    transform.position += new Vector3(0.1f, 0.0f, 0.0f);
        //}
        //if (Input.GetKey(KeyCode.S))
        //{
        //    transform.position += new Vector3(-0.1f, 0.0f, 0.0f);
        //}
        //if (Input.GetKey(KeyCode.A))
        //{
        //    transform.position += new Vector3(0.0f, 0.0f, 0.1f);
        //}
        //if (Input.GetKey(KeyCode.D))
        //{
        //    transform.position += new Vector3(0.0f, 0.0f, -0.1f);
        //}
    }
}
