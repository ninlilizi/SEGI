using UnityEngine;
using System.Collections;

public class Torchslider2 : MonoBehaviour {
	public GameObject TorcheObj;
	public Camera MainCamera;
	public GUISkin SkinSlider;
	private GameObject Torch;
	private float Intensity_Light;
	private bool CameraRendering;

	
    void OnGUI() {
		GUI.Label(new Rect(25,25,150,30),"Light Intensity",SkinSlider.label);
		Intensity_Light= GUI.HorizontalSlider(new Rect(25, 50, 150, 30), Intensity_Light, 0.0F, TorcheObj.GetComponent<Torchelight>().MaxLightIntensity,SkinSlider.horizontalSlider,SkinSlider.horizontalSliderThumb);
		CameraRendering=GUI.Toggle(new Rect(25,80,150,30),CameraRendering,"Deferred lighting");
		if (CameraRendering==true) {
			MainCamera.renderingPath=RenderingPath.DeferredLighting;
		}
		else {
			MainCamera.renderingPath=RenderingPath.Forward;
		}
		

	}
	
	void Update() {
		foreach (GameObject i in GameObject.FindGameObjectsWithTag("TagLight")) {
			i.GetComponent<Torchelight>().IntensityLight=Intensity_Light;
		}
	}
}
