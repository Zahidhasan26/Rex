using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace BowGame
{
    // Standing idle for a model that only has a run clip (the Gorgosaurus).
    // When the dinosaur stands still or is attacking (spitting), it blends from the run cycle into a
    // planted standing pose built from two frames of the run clip (one per leg, each with that foot down),
    // and adds slow breathing and a lazy tail sway. Runs before ProceduralDinoAnim, so the attack
    // jaw/head motion, hit flinch and death are layered on top of this pose.
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Dinosaur))]
    public class DinoIdlePose : MonoBehaviour
    {
        [Header("Pose (normalized times in the run clip)")]
        [Tooltip("Frame used for the whole body and the left leg (left foot planted under the body).")]
        [Range(0f, 1f)] public float poseTime = 0.5f;
        [Tooltip("Frame used for the leg below 'Second Leg Root' (that foot planted under the body).")]
        [Range(0f, 1f)] public float secondLegTime = 0.0f;
        [Tooltip("Top bone of the leg that takes its pose from the second frame (e.g. backHip_R).")]
        public Transform secondLegRoot;

        [Header("When")]
        [Tooltip("Below this speed (m/s) the dinosaur counts as standing.")]
        public float standingSpeed = 0.4f;
        [Tooltip("Blend in/out speed (1 = one second).")]
        public float blendSpeed = 4f;

        [Header("Breathing")]
        public Transform[] breathBones;
        public float breathAngle = 1.5f;
        public float breathPeriod = 2.6f;
        public Transform[] tailBones;
        public float tailSwayAngle = 2.5f;
        public float tailSwayPeriod = 3.6f;

        Dinosaur dino;
        NavMeshAgent agent;
        Animator animator;
        Transform[] bones;
        Quaternion[] poseRot;
        Vector3[] posePos;
        float weight;
        float phase;

        void Awake()
        {
            dino = GetComponent<Dinosaur>();
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponentInChildren<Animator>();
            phase = Random.Range(0f, 10f);
            BakePose();
        }

        void BakePose()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0) return;
            var clip = clips[0];

            bones = animator.GetComponentsInChildren<Transform>(true);
            poseRot = new Quaternion[bones.Length];
            posePos = new Vector3[bones.Length];

            // Remember the current pose so baking leaves no trace.
            var keepRot = new Quaternion[bones.Length];
            var keepPos = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) { keepRot[i] = bones[i].localRotation; keepPos[i] = bones[i].localPosition; }

            clip.SampleAnimation(animator.gameObject, poseTime * clip.length);
            for (int i = 0; i < bones.Length; i++) { poseRot[i] = bones[i].localRotation; posePos[i] = bones[i].localPosition; }

            if (secondLegRoot != null)
            {
                clip.SampleAnimation(animator.gameObject, secondLegTime * clip.length);
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] == secondLegRoot || bones[i].IsChildOf(secondLegRoot))
                    { poseRot[i] = bones[i].localRotation; posePos[i] = bones[i].localPosition; }
            }

            for (int i = 0; i < bones.Length; i++) { bones[i].localRotation = keepRot[i]; bones[i].localPosition = keepPos[i]; }
        }

        void LateUpdate()
        {
            if (bones == null) return;

            if (!dino.IsDead)   // after death, hold whatever blend we had so the body doesn't snap
            {
                float speed = agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
                bool standing = speed < standingSpeed || dino.IsAttacking;
                weight = Mathf.MoveTowards(weight, standing ? 1f : 0f, blendSpeed * Time.deltaTime);
            }
            if (weight <= 0.001f) return;

            float w = Mathf.SmoothStep(0f, 1f, weight);
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i];
                if (b == null || b == animator.transform) continue;
                b.localRotation = Quaternion.Slerp(b.localRotation, poseRot[i], w);
                b.localPosition = Vector3.Lerp(b.localPosition, posePos[i], w);
            }

            if (dino.IsDead) return;
            float t = Time.time + phase;
            float breath = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, breathPeriod)) * breathAngle * w;
            if (breathBones != null)
                foreach (var b in breathBones) if (b != null) b.rotation = Quaternion.AngleAxis(breath, transform.right) * b.rotation;
            float sway = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, tailSwayPeriod)) * tailSwayAngle * w;
            if (tailBones != null)
                foreach (var b in tailBones) if (b != null) b.rotation = Quaternion.AngleAxis(sway, transform.up) * b.rotation;
        }
    }
}
