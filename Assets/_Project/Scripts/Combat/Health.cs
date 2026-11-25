using System;
using System.Collections.Generic;
using ShapeShifty.Audio;
using UnityEngine;

namespace ShapeShifty.Combat
{
    public enum Team { Player, Greys }

    // Hit points for anything that can be splatted: the player, the Grey troop's monkeys and
    // their king. Only hits from the other team count (see PooProjectile). A short spell of
    // grace after each hit stops one volley taking it all at once.
    public class Health : MonoBehaviour
    {
        [SerializeField] Team team = Team.Greys;
        [SerializeField] float max = 3f;
        [Tooltip("Seconds after a hit when further hits don't count.")]
        [SerializeField] float graceTime = 0f;

        public static readonly List<Health> All = new List<Health>();
        // Anyone left in the level the player can fight.
        public static bool AnyFoes
        {
            get
            {
                foreach (var h in All) if (h.team != Team.Player && !h.IsDead) return true;
                return false;
            }
        }

        public Team Team => team;
        public float Max => max;
        public float Current { get; private set; }
        public float Fraction => max > 0f ? Current / max : 0f;
        public bool IsDead => Current <= 0f;

        // Amount, and where it came from.
        public event Action<float, Vector3> Damaged;
        public event Action Healed;
        public event Action Died;

        float graceUntil;

        void Awake() => Current = max;
        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        // Hits don't count while set, e.g. for the King until he's up off his throne.
        public bool Invulnerable { get; set; }

        public bool CanBeHitBy(Team attacker) => attacker != team && !IsDead && !Invulnerable && Time.time >= graceUntil;

        public void Damage(float amount, Vector3 from, Team attacker)
        {
            if (!CanBeHitBy(attacker) || amount <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            graceUntil = Time.time + graceTime;
            Damaged?.Invoke(amount, from);
            if (team == Team.Player) Sfx.Play(Sound.Hurt, transform.position);
            else Sfx.Play(Current <= 0f ? Sound.Defeat : Sound.Squeak, transform.position);
            if (Current <= 0f) Died?.Invoke();
        }

        // Returns false if already full.
        public bool Heal(float amount)
        {
            if (IsDead || Current >= max) return false;
            Current = Mathf.Min(max, Current + amount);
            Healed?.Invoke();
            Sfx.Play(Sound.Pickup, transform.position);
            return true;
        }
    }
}
