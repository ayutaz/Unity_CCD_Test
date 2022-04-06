using UnityEngine;

namespace _CCD_Test
{
    [CreateAssetMenu(fileName = "MyData", menuName = "MyData", order = 0)]
    public class MyData : ScriptableObject
    {
        public string ccdBaseURL;
    }
}