// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ButtonExt.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UltimateXR.Extensions.System.Threading;
using UnityEngine.UI;


namespace UltimateXR.Extensions.Unity.UI
{
    /// <summary>
    ///     <see cref="Button" /> extensions.
    /// </summary>
    public static class ButtonExt
    {
        #region Public Methods

        /// <summary>
        ///     Asynchronously waits until a <see cref="Button" /> is clicked.
        /// </summary>
        /// <param name="self">Button to wait for</param>
        /// <param name="ct">Optional cancellation token, to cancel the task</param>
        /// <returns>Awaitable task that will finish once the button was clicked or the <see cref="Task" /> was canceled</returns>
        public static async Task WaitForClickAsync(this Button self, CancellationToken ct = default)
        {
            var isClicked = false;


            void ButtonClicked()
            {
                isClicked = true;
            }


            self.onClick.AddListener(ButtonClicked);
            await TaskExt.WaitUntil(() => isClicked, ct);
            self.onClick.RemoveListener(ButtonClicked);
        }


        /// <summary>
        ///     Asynchronously waits until a <see cref="Button" /> is clicked. Returns the <see cref="Button" /> that was clicked.
        /// </summary>
        /// <param name="self">Button to wait for</param>
        /// <param name="ct">Optional cancellation token, to cancel the task</param>
        /// <returns>
        ///     Awaitable task that will finish once the button was clicked or the <see cref="Task" /> was canceled, and that
        ///     returns the <see cref="Button" /> that was clicked
        /// </returns>
        public static async Task<Button> ReadAsync(this Button self, CancellationToken ct)
        {
            await self.WaitForClickAsync(ct);

            return ct.IsCancellationRequested ? null : self;
        }


        /// <summary>
        ///     Asynchronously waits until a <see cref="Button" /> in a set is clicked. Returns the <see cref="Button" /> that was
        ///     clicked.
        /// </summary>
        /// <param name="ct">Cancellation token, to cancel the task</param>
        /// <param name="buttons">Buttons to wait for</param>
        /// <returns>
        ///     Awaitable task that will finish once a button was clicked or the <see cref="Task" /> was canceled, and that
        ///     returns the <see cref="Button" /> that was clicked
        /// </returns>
        public static Task<Button> ReadAsync(CancellationToken ct, params Button[] buttons)
        {
            return buttons.ReadAsync(ct);
        }


        /// <summary>
        ///     Asynchronously waits until a <see cref="Button" /> in a set is clicked. Returns the <see cref="Button" /> that was
        ///     clicked.
        /// </summary>
        /// <param name="buttons">Buttons to wait for</param>
        /// <param name="ct">Optional cancellation token, to cancel the task</param>
        /// <returns>
        ///     Awaitable task that will finish once a button was clicked or the <see cref="Task" /> was canceled, and that
        ///     returns the <see cref="Button" /> that was clicked
        /// </returns>
        public static async Task<Button> ReadAsync(this Button[] buttons, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tasks = buttons.Select(b => b.ReadAsync(ct));
            var finishedTask = await Task.WhenAny(tasks);

            if (!finishedTask.IsCanceled)
            {
                cts.Cancel();
            }

            return await finishedTask;
        }

        #endregion
    }
}