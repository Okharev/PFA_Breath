using UnityEngine;

namespace Dialogues
{
    [CreateAssetMenu(fileName = "NewSpeaker", menuName = "Dialogue/Speaker")]
    public class Speaker : ScriptableObject
    {
        public string speakerName;
        public Color speakerColor = Color.white;
        public Sprite portrait;
    }
}