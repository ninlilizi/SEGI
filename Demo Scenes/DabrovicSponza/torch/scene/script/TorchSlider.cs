using UnityEngine;
using System.Collections;

public class TorchSlider : MonoBehaviour {
	public GameObject TorcheObj;
	public GUISkin SkinSlider;
	
    void OnGUI() {
		GUI.Label(new Rect(25,25,150,30),"Light Intensity",SkinSlider.label);
        TorcheObj.GetComponent<Torchelight>().IntensityLight = GUI.HorizontalSlider(new Rect(25, 50, 150, 30), TorcheObj.GetComponent<Torchelight>().IntensityLight, 0.0F, TorcheObj.GetComponent<Torchelight>().MaxLightIntensity,SkinSlider.horizontalSlider,SkinSlider.horizontalSliderThumb);
    }
}