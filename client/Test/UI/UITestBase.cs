using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Diagnostics;

namespace Test.UI
{
    public class UITestBase : IDisposable
    {
        private Application app;
        private UIA3Automation automation;
        private Window mainWindow;
        private bool disposed = false;

        public UITestBase()
        {
            app = Application.Launch(@"C:\Users\eugen\Documents\Tecnologías\PASSWORD LIS\client\PASSWORD LIS Client\bin\Debug\PASSWORD LIS Client.exe");
            automation = new UIA3Automation();
            mainWindow = app.GetMainWindow(automation);

            WaitForElement(() => FindElementByText("logInText"), TimeSpan.FromSeconds(10));
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Liberar recursos administrados
                    if (app != null)
                    {
                        if (!app.HasExited)
                        {
                            try
                            {
                                app.Close();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Warning closing application: {ex.Message}");
                            }
                        }
                        app.Dispose();
                        app = null;
                    }

                    automation?.Dispose();
                    automation = null;
                    mainWindow = null;
                }

                // Aquí irían recursos no administrados si los hubiera

                disposed = true;
            }
        }

        private void CheckDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }


        // Métodos de búsqueda de elementos
        protected AutomationElement FindElementById(string automationId)
        {
            CheckDisposed();
            return mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        }

        protected AutomationElement FindElementByText(string text)
        {
            CheckDisposed();
            return mainWindow.FindFirstDescendant(cf => cf.ByText(text));
        }

        protected AutomationElement FindElementByName(string name)
        {
            CheckDisposed();
            return mainWindow.FindFirstDescendant(cf => cf.ByName(name));
        }

        protected AutomationElement FindElementByControlType(FlaUI.Core.Definitions.ControlType controlType)
        {
            CheckDisposed();
            return mainWindow.FindFirstDescendant(cf => cf.ByControlType(controlType));
        }

        // Métodos de interacción
        protected void EnterText(string automationId, string text)
        {
            CheckDisposed();
            var element = FindElementById(automationId)?.AsTextBox();
            element?.Enter(text);
        }

        protected void ClickElement(string automationId)
        {
            CheckDisposed();
            var element = FindElementById(automationId);
            element?.AsButton()?.Click();
        }

        protected void ClickElementByText(string text)
        {
            CheckDisposed();
            var element = FindElementByText(text);
            if (element != null && !element.IsOffscreen)
            {
                element.Click();
            }
        }

        protected void ClickHyperlinkByText(string linkText)
        {
            CheckDisposed();
            var hyperlinkElement = mainWindow.FindFirstDescendant(cf => cf.ByText(linkText));

            if (hyperlinkElement != null && !hyperlinkElement.IsOffscreen)
            {
                // Intentar como botón primero
                var asButton = hyperlinkElement.AsButton();
                if (asButton != null)
                {
                    asButton.Click();
                    return;
                }

                // Intentar click directo
                hyperlinkElement.Click();
            }
        }

        protected string GetElementText(string automationId)
        {
            CheckDisposed();
            var element = FindElementById(automationId)?.AsTextBox();
            return element?.Text ?? string.Empty;
        }

        protected bool IsElementVisible(string automationId)
        {
            CheckDisposed();
            var element = FindElementById(automationId);
            return element != null && !element.IsOffscreen;
        }

        // Métodos de espera
        protected static AutomationElement WaitForElement(Func<AutomationElement> findElement, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                var element = findElement();
                if (element != null && !element.IsOffscreen)
                    return element;

                // Espera corta pero no bloqueante
                System.Threading.Tasks.Task.Delay(50).Wait();
            }

            return null;
        }

        protected static bool WaitForCondition(Func<bool> condition, TimeSpan timeout)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                if (condition())
                    return true;

                System.Threading.Tasks.Task.Delay(50).Wait();
            }

            return false;
        }

        protected void WaitForElementToBeClickable(string automationId, TimeSpan timeout)
        {
            WaitForCondition(() =>
            {
                var element = FindElementById(automationId);
                return element != null && !element.IsOffscreen && element.IsEnabled;
            }, timeout);
        }

        protected static void WaitForElementToDisappear(Func<AutomationElement> findElement, TimeSpan timeout)
        {
            WaitForCondition(() =>
            {
                var element = findElement();
                return element == null || element.IsOffscreen;
            }, timeout);
        }

        // Métodos de navegación corregidos
        protected void NavigateToSignUp()
        {
            ClickHyperlinkByText("signUpText");
            WaitForElement(() => FindElementById("firstNameTextBox"), TimeSpan.FromSeconds(5));
        }

        protected void NavigateToForgotPassword()
        {
            ClickHyperlinkByText("forgotUserPasswordText");
            WaitForElement(() => FindElementById("emailTextBox"), TimeSpan.FromSeconds(5));
        }

        protected void NavigateBackToLogin()
        {
            ClickElementByText("signUpText");
            WaitForElement(() => FindElementById("loginText"), TimeSpan.FromSeconds(5));
        }

        // Métodos de interacción seguros
        protected void SafeClick(string automationId, TimeSpan? timeout = null)
        {
            if (!timeout.HasValue)
            {
                timeout = TimeSpan.FromSeconds(5);
            }

            WaitForElementToBeClickable(automationId, timeout.Value);
            ClickElement(automationId);
        }

        protected void SafeEnterText(string automationId, string text, TimeSpan? timeout = null)
        {
            if (!timeout.HasValue)
            {
                timeout = TimeSpan.FromSeconds(5);
            }

            var element = WaitForElement(() => FindElementById(automationId), timeout.Value);
            element?.AsTextBox()?.Enter(text);
        }

    }
}
