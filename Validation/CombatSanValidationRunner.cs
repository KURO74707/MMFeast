using System.Collections;
using UnityEngine;
public class CombatSanValidationRunner : MonoBehaviour
{
    public void Begin(IEnumerator routine) => StartCoroutine(routine);
}
