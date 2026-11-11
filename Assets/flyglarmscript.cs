using UnityEngine;
using System.Collections;


public class flyglarmscript : MonoBehaviour
{
   public AudioSource source;
   public float startTime = 3f;
   public float duration = 3f;




   // Start is called once before the first execution of Update after the MonoBehaviour is created
   void Start()
   {
       StartCoroutine(Run());


      
   }


   IEnumerator Run(){
       source.time=startTime;
       source.Play();
       yield return new WaitForSeconds(duration);
       source.Stop();
   }
}





