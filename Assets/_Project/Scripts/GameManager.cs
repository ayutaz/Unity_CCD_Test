using System.IO;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace _CCD_Test
{
    public class GameManager : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] private MyData myData;
        private const string JsonURL = "generated.json";
        private const string PNGURL = "snapshot.png";
        private const string FBXURL = "PT_Fruit_Tree_01_green.fbx";
        [SerializeField] private RawImage rawImage;
        [SerializeField] private TextMeshProUGUI jsonText;

        private void Start()
        {
            GetJson();
            GetPNG();
            GetFBX();
        }

        private async void GetFBX()
        {
            var fbx = UnityWebRequest.Get(Path.Combine(myData.ccdBaseURL, FBXURL));
            Debug.Log(Path.Combine(myData.ccdBaseURL, FBXURL));
            await fbx.SendWebRequest();
            Debug.Log("FBX: " + fbx.downloadHandler);
        }

        private async void GetPNG()
        {
            var texture = UnityWebRequestTexture.GetTexture(Path.Combine(myData.ccdBaseURL, PNGURL));
            await texture.SendWebRequest();
            rawImage.texture = ((DownloadHandlerTexture)texture.downloadHandler).texture;
        }

        private async void GetJson()
        {
            var json = UnityWebRequest.Get(Path.Combine(myData.ccdBaseURL, JsonURL));
            await json.SendWebRequest();
            jsonText.text = json.downloadHandler.text;
        }
    }
}