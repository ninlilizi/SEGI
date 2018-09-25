using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[ExecuteInEditMode]
public class SEGI_FollowTransform : MonoBehaviour {

    void Start()
    {
        StopCoroutine(UpdateFollowField());
        StartCoroutine(UpdateFollowField());
    }

    IEnumerator UpdateFollowField()
    {
        do
        {
            SEGI_NKLI.followTransform = GetComponent<Transform>();
            yield return new WaitForSeconds(1);
        } while (true);
    }
}
