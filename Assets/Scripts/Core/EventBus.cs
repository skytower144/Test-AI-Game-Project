using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chopper
{
    /// <summary>이벤트 버스로 전달되는 모든 이벤트의 마커 인터페이스.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// 타입 기반 전역 이벤트 버스.
    /// 발행자와 구독자가 서로를 참조하지 않고 이벤트 구조체만으로 통신한다.
    /// 핸들러 배열은 copy-on-write 방식이라 발행 도중 구독/해지가 일어나도 안전하다.
    /// </summary>
    public static class EventBus
    {
        private static readonly List<Action> s_ClearActions = new List<Action>();

        private static class Channel<T> where T : struct, IGameEvent
        {
            public static Action<T>[] Handlers = Array.Empty<Action<T>>();

            static Channel()
            {
                s_ClearActions.Add(() => Handlers = Array.Empty<Action<T>>());
            }
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;

            Action<T>[] current = Channel<T>.Handlers;
            if (Array.IndexOf(current, handler) >= 0) return; // 중복 구독 방지

            var next = new Action<T>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = handler;
            Channel<T>.Handlers = next;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) return;

            Action<T>[] current = Channel<T>.Handlers;
            int index = Array.IndexOf(current, handler);
            if (index < 0) return;

            if (current.Length == 1)
            {
                Channel<T>.Handlers = Array.Empty<Action<T>>();
                return;
            }

            var next = new Action<T>[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);
            Channel<T>.Handlers = next;
        }

        public static void Publish<T>(T evt) where T : struct, IGameEvent
        {
            Action<T>[] handlers = Channel<T>.Handlers;
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](evt);
                }
                catch (Exception e)
                {
                    // 한 구독자의 예외가 다른 구독자에게 전파되지 않도록 격리한다.
                    Debug.LogException(e);
                }
            }
        }

        /// <summary>모든 채널의 구독자를 제거한다.</summary>
        public static void ClearAll()
        {
            for (int i = 0; i < s_ClearActions.Count; i++)
                s_ClearActions[i]();
        }

        // Enter Play Mode 옵션(도메인 리로드 비활성화)에서도 이전 플레이의 구독이 남지 않도록 초기화.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ClearAll();
        }
    }
}
