using System.Collections;
using UnityEngine;

public class MenuValidationRunner : MonoBehaviour
{
    public void Begin(IEnumerator routine) { DontDestroyOnLoad(gameObject); StartCoroutine(routine); }
}
