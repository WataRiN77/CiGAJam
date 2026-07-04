using UnityEngine;
using UnityEngine.Assertions;
using Febucci.UI.Core;

namespace Febucci.UI.Examples
{
    [AddComponentMenu("Febucci/TextAnimator/WwiseSoundWriter")]
    [RequireComponent(typeof(TypewriterCore))]
    public class TAnimSoundWriter : MonoBehaviour
    {
        [Header("Wwise Events")]
        public AK.Wwise.Event startEvent;   // 打字开始时触发
        public AK.Wwise.Event endEvent;     // 打字结束时（全部显示完）触发

        private TypewriterCore typewriter;

        private void Awake()
        {
            typewriter = GetComponent<TypewriterCore>();
            Assert.IsNotNull(typewriter, "TypewriterCore component missing.");
            Assert.IsTrue(startEvent.IsValid(), "Start Wwise Event is invalid.");
            Assert.IsTrue(endEvent.IsValid(), "End Wwise Event is invalid.");

            // 订阅开始和结束事件
            typewriter.onTypewriterStart.AddListener(OnTypewriterStart);
            typewriter.onTextShowed.AddListener(OnTextShowed);
        }

        private void OnTypewriterStart()
        {
            startEvent.Post(gameObject);
        }

        private void OnTextShowed()
        {
            endEvent.Post(gameObject);
        }

        private void OnDestroy()
        {
            if (typewriter != null)
            {
                typewriter.onTypewriterStart.RemoveListener(OnTypewriterStart);
                typewriter.onTextShowed.RemoveListener(OnTextShowed);
            }
        }
    }
}