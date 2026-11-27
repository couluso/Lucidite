using UnityEngine;

namespace Lucidite
{
    /// <summary>
    /// Carte lâchée (la Dépression s'effondre) : chute balistique, petite rotation, puis elle se pose à plat
    /// sur la table (ou au sol) et glisse un peu. Simulée dans le repère de la pièce pour suivre ses secousses.
    /// </summary>
    public class FallingCard : MonoBehaviour
    {
        Vector3 _vel;
        Quaternion _from, _to;
        float _t, _fallTime;
        float _surface;
        bool _landed;
        Transform _room;
        BloodSystem _bs;

        public static void Drop(CardView v, Vector3 push, int stackIndex)
        {
            if (!v) return;
            v.enabled = false; // plus de déplacement vers sa cible
            var f = v.gameObject.AddComponent<FallingCard>();
            f._bs = BloodSystem.Instance;
            f._room = f._bs ? f._bs.puddleRoot : null;
            if (f._room) v.transform.SetParent(f._room, true);
            f._vel = push;
            f._from = v.transform.localRotation;
            // Retombe à plat, face cachée ou visible au hasard, orientée n'importe comment
            var yaw = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            bool faceDown = Random.value < 0.6f;
            f._to = Quaternion.LookRotation(faceDown ? Vector3.up : Vector3.down, yaw);
            f._surface = stackIndex * 0.0007f;
        }

        void Start()
        {
            var lp = transform.localPosition;
            float h = Mathf.Max(0.05f, lp.y - SurfaceAt(lp));
            _fallTime = Mathf.Sqrt(2f * h / 9.81f) + 0.05f;
        }

        float SurfaceAt(Vector3 lp)
        {
            if (!_bs) return 0f;
            bool overTable = Mathf.Abs(lp.x) < _bs.tableHalfSize.x && Mathf.Abs(lp.z) < _bs.tableHalfSize.y;
            return (overTable ? _bs.tableY + 0.0015f : _bs.floorY + 0.001f) + _surface;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var lp = transform.localPosition;
            if (!_landed)
            {
                _t += dt;
                _vel += Physics.gravity * dt;
                lp += _vel * dt;
                float s = SurfaceAt(lp);
                // Bascule à plat pendant la chute (avec une petite vrille)
                float k = Mathf.Clamp01(_t / _fallTime);
                transform.localRotation = Quaternion.Slerp(_from, _to, k * k * (3f - 2f * k)) * Quaternion.Euler(0f, 0f, Mathf.Sin(k * Mathf.PI) * 25f);
                if (lp.y <= s)
                {
                    lp.y = s;
                    _landed = true;
                    _vel = new Vector3(_vel.x, 0f, _vel.z) * 0.35f;
                    transform.localRotation = _to;
                    AudioManager.Play(Sfx.CardPlace, 0.5f);
                }
            }
            else
            {
                // Glisse sur le plateau puis s'arrête
                _vel = Vector3.MoveTowards(_vel, Vector3.zero, dt * 1.6f);
                lp += _vel * dt;
                lp.y = SurfaceAt(lp);
                if (_vel.sqrMagnitude < 1e-6f) enabled = false;
            }
            transform.localPosition = lp;
        }
    }
}
