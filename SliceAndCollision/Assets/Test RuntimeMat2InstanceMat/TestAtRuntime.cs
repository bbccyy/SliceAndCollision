using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TestAtRuntime : MonoBehaviour
{

    private GameObject go; 
    private GameObject igo1; 
    private GameObject igo2; 

    int ct = 1200;

    private void OnEnable()
    {
        Debug.Log("on enable");
    }

    // Start is called before the first frame update
    void Start()
    {
        go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Src/Outputs/S1/img_reboundBall_HaiLunNa_17.prefab");

        igo1 = Instantiate(go);
        var rd = igo1.GetComponent<MeshRenderer>();
        rd.material.SetColor("_Color", Color.blue);

        igo2 = Instantiate(go);
        //var rd2 = igo2.GetComponent<MeshRenderer>();
        //rd2.material.SetColor("_Color", Color.blue);
        igo2.transform.position += new Vector3(3, 0, 0);

        Debug.Log("on start");
    }


    // Update is called once per frame
    void Update()
    {
        ct--;
        if (ct == 0)
        {
            var rd2 = igo2.GetComponent<MeshRenderer>();
            rd2.material.SetColor("_Color", Color.blue);
        }
    }
}
