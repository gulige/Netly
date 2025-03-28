﻿using System;
using System.Collections.Generic;

namespace Netly.Core
{
    /// <summary>
    /// It is an event execution bridge
    /// </summary>
    public static class Call
    {
        /// <summary>
        /// Events are said to be dispatched automatically
        /// </summary>
        public static bool Automatic { get; set; } = true;
        private static List<Action> _actions { get; set; } = new List<Action>();
        private static Object actionListLock = new object ();

        /// <summary>
        /// Add an event to the execution queue
        /// </summary>
        /// <param name="action">Event or callback</param>
        public static void Execute(Action action)
        {
            if (action == null)
            {
                return;
            }
            else if (Automatic)
            {
                action();
            }
            else
            {
                lock(actionListLock) {
                    _actions.Add(action);
                }
            }
        }

        /// <summary>
        /// Dispatch the events, which are in the queue, (Oly if "Automatic" is "False")
        /// </summary>
        public static void Publish()
        {
            if (Automatic)
            {
                return;
            }
            else if (_actions.Count > 0)
            {
                Action[] actionArray;
                lock (actionListLock) {
                    actionArray = _actions.ToArray ();
                    _actions.Clear ();
                }

                foreach (var action in actionArray)
                {
                    action ();
                }
            }
        }
    }
}
