using ItemChanger.Extensions;
using System;
using System.Collections;
using UnityEngine;

namespace SpicyRando.Util;

internal class Dummy : MonoBehaviour
{
    public new Coroutine StartCoroutine(IEnumerator coroutine) => base.StartCoroutine(coroutine);
}

internal static class GameObjectExtensions
{
    internal static void DoAfter(this GameObject go, Action action, float delay)
    {
        IEnumerator Routine()
        {
            yield return new WaitForSeconds(delay);
            action();
        }
        go.GetOrAddComponent<Dummy>().StartCoroutine(Routine());
    }
}
