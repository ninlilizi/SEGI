using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[ExecuteInEditMode]
public class SEGI_SunLight : MonoBehaviour {

	void OnEnable ()
    {
        StopCoroutine(UpdateSunField());
        StartCoroutine(UpdateSunField());
    }

    IEnumerator UpdateSunField()
    {
        do
        {
            SEGICascaded.Sun = GetComponent<Light>();
            yield return new WaitForSeconds(1);
        } while (true);
    }
}
