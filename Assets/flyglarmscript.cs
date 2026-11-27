using UnityEngine;
using System.Collections;


public class flyglarmscript : MonoBehaviour
{
   public AudioSource source;
   public float startTime = 3f;
   public float duration = 3f;
   [Tooltip("Hur många sekunder i slutet som ljudet tonas ut")]
   public float fadeOutSeconds = 1.5f;




   // Start is called once before the first execution of Update after the MonoBehaviour is created
   void Start()
   {
       StartCoroutine(Run());


      
   }


   IEnumerator Run(){
       source.time=startTime;
       source.Play();
       float fade = Mathf.Clamp(fadeOutSeconds, 0f, duration);
       yield return new WaitForSeconds(duration - fade);
       // Tona ut mjukt i stället för att stänga av tvärt
       float startVolume = source.volume;
       for (float t = 0f; t < fade; t += Time.deltaTime)
       {
           source.volume = Mathf.Lerp(startVolume, 0f, t / fade);
           yield return null;
       }
       source.Stop();
       source.volume = startVolume;
   }
}





