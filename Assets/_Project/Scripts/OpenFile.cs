using System.IO;
using SFB;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace _CCD_Test
{
    public class OpenFile : MonoBehaviour
    {
        [SerializeField] private Button selectFileButton;
        [SerializeField] private Button uploadFilesButton;
        [SerializeField] private TextMeshProUGUI filePathText;
        [SerializeField] private TextMeshProUGUI fileNameText;

        private void Awake()
        {
            selectFileButton.OnClickAsObservable().Subscribe(_ =>
            {
                var pathList = StandaloneFileBrowser.OpenFilePanel("Title", "", "txt", false);
                foreach (var path in pathList)
                {
                    filePathText.text = $"file path : {path}\n";
                    fileNameText.text = $"file name: {GetFileName(path)}";
                }
            }).AddTo(this);
        }

        private static string GetFileName(string filePath)
        {
            return Path.GetFileName(filePath);
        }
    }
}