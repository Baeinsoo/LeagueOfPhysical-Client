using System;
using UnityEngine;

namespace LOP
{
    /// <summary>결과 씬 소리 창구(spec §7). 결과판은 이것만 안다 — 시험은 가짜를 꽂는다.</summary>
    public interface IResultSound
    {
        void PlayNewBest();
    }

    /// <summary>
    /// 결과 씬 NEW BEST 소리(spec §6). 경기 소리(<see cref="FlappyAudio"/>)는 경기 스코프라 결과 씬에서 못 쓴다 — 같은 조리법
    /// (<see cref="FlappySynth.Render"/>)으로 클립 하나만, 처음 부를 때 짓는다. 2D 소리, 기기 음량을 따른다.
    /// </summary>
    public sealed class ResultSound : IResultSound, IDisposable
    {
        private GameObject _root;
        private AudioSource _source;
        private AudioClip _clip;
        private bool _disposed;

        //  시험 이음매 — 실제로는 AudioSource.PlayOneShot.
        internal Action<AudioClip> playOneShot;

        public ResultSound()
        {
            playOneShot = clip =>
            {
                EnsureSource();
                if (_source != null && clip != null)
                {
                    _source.PlayOneShot(clip);
                }
            };
        }

        internal AudioClip Clip
        {
            get
            {
                if (_clip == null && _disposed == false)
                {
                    float[] data = FlappySynth.Render(FlappySfx.NewBest);
                    _clip = AudioClip.Create("ResultNewBest", data.Length, 1, FlappySynth.Rate, false);
                    _clip.SetData(data, 0);
                }
                return _clip;
            }
        }

        public void PlayNewBest()
        {
            if (_disposed)
            {
                return;
            }
            playOneShot(Clip);
        }

        private void EnsureSource()
        {
            if (_root != null)
            {
                return;
            }
            _root = new GameObject("ResultSound");
            _source = _root.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
        }

        public void Dispose()
        {
            _disposed = true;
            DestroyObject(_root);
            DestroyObject(_clip);
            _root = null;
            _source = null;
            _clip = null;
        }

        private static void DestroyObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }
            if (UnityEngine.Application.isPlaying) { UnityEngine.Object.Destroy(obj); }
            else { UnityEngine.Object.DestroyImmediate(obj); }
        }
    }
}
