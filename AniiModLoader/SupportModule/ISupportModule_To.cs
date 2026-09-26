using System.Collections;

namespace AniiModLoader
{
    public interface ISupportModule_To
    {
        object StartCoroutine(IEnumerator coroutine);
        void StopCoroutine(object coroutineToken);
        void UnityDebugLog(string msg);
    }
}