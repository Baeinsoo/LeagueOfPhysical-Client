using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 소리 파일 칸(spec §6). 칸이 비어 있으면 <see cref="FlappySynth"/>가 지은 임시 클립을 쓰고, 파일을 꽂으면 그게 이긴다.
    /// 지금은 에셋을 만들지 않는다(경기 스코프의 칸이 비어 있다) — 외주 소리가 오면 에셋 하나 만들어 꽂으면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "LOP/Flappy/Sound Bank", fileName = "FlappySoundBank")]
    public class FlappySoundBank : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public FlappySfx sfx;
            public AudioClip clip;
        }

        [SerializeField] private Entry[] sfx = Array.Empty<Entry>();
        [SerializeField] private AudioClip music;

        /// <summary>배경음 파일. 없으면 null.</summary>
        public AudioClip Music => music;

        /// <summary>이 소리에 꽂힌 파일. 없으면 null.</summary>
        public AudioClip ClipFor(FlappySfx target)
        {
            foreach (var entry in sfx)
            {
                if (entry.sfx == target && entry.clip != null)
                {
                    return entry.clip;
                }
            }
            return null;
        }

        //  시험용 — 인스펙터에서 꽂는 것과 같다.
        internal void Set(FlappySfx target, AudioClip clip)
        {
            var list = new List<Entry>(sfx) { new Entry { sfx = target, clip = clip } };
            sfx = list.ToArray();
        }

        internal void SetMusic(AudioClip clip) => music = clip;
    }

    /// <summary>
    /// 경기 스코프의 인스펙터 칸(비어 있을 수 있다)을 넘기는 그릇. VContainer는 null 인스턴스를 등록할 수 없어서
    /// 칸 대신 그릇을 등록한다.
    /// </summary>
    public sealed class FlappySoundBankSlot
    {
        public FlappySoundBankSlot(FlappySoundBank bank)
        {
            Bank = bank;
        }

        public FlappySoundBank Bank { get; }
    }
}
