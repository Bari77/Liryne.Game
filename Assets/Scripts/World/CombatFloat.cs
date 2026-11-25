using UnityEngine;

namespace Liryne.Game.World
{
    sealed class CombatFloat : MonoBehaviour
    {
        TextMesh _text;
        Color _color;
        float _age;
        const float Life = 0.85f;

        public void Bind(string caption, Color color)
        {
            _text = GetComponent<TextMesh>();
            _color = color;
            if (_text is not null)
            {
                _text.text = caption;
                _text.color = color;
            }
        }

        void LateUpdate()
        {
            _age += Time.deltaTime;
            transform.localPosition += Vector3.up * (1.6f * Time.deltaTime);
            if (_text is not null)
            {
                Color fade = _color;
                fade.a = Mathf.Clamp01(1f - (_age / Life));
                _text.color = fade;
            }

            Camera camera = Camera.main;
            if (camera is not null)
            {
                WorldLook.AwayFrom(transform, camera.transform.position);
            }

            if (_age >= Life)
            {
                Destroy(gameObject);
            }
        }
    }
}
