using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System;
using System.Diagnostics;
using FlaUI.Core.Conditions;
using FlaUI.Core.Input;

namespace Test.UI
{
    public class UITestBase : IDisposable
    {
        private Application app;
        private UIA3Automation automation;
        private Window mainWindow; // kept for backwards compatibility but we will search all windows
        private bool disposed = false;

        public UITestBase()
        {
            app = Application.Launch(@"C:\Users\eugen\Documents\Tecnologías\PASSWORD LIS\client\PASSWORD LIS Client\bin\Debug\PASSWORD LIS Client.exe");
            automation = new UIA3Automation();
            mainWindow = app.GetMainWindow(automation);

            // Wait for login button instead of localized text (more stable)
            WaitForElement(() => FindElementById("loginButton"), TimeSpan.FromSeconds(15));
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

        private Window[] GetAllWindows()
        {
            try
            {
                return app?.GetAllTopLevelWindows(automation) ?? Array.Empty<Window>();
            }
            catch
            {
                return Array.Empty<Window>();
            }
        }

        private AutomationElement FindInAllWindows(Func<ConditionFactory, ConditionBase> conditionBuilder)
        {
            CheckDisposed();
            foreach (var win in GetAllWindows())
            {
                var el = win.FindFirstDescendant(conditionBuilder);
                if (el != null) return el;
            }
            return null;
        }

        // ---- Element Finders ----
        protected AutomationElement FindElementById(string automationId)
        {
            return FindInAllWindows(cf => cf.ByAutomationId(automationId));
        }

        protected AutomationElement FindElementByText(string text)
        {
            // Search by UIA Name (AutomationProperties.Name / control text)
            return FindInAllWindows(cf => cf.ByName(text));
        }

        protected AutomationElement FindElementByName(string name)
        {
            return FindElementByText(name);
        }

        protected AutomationElement FindElementByControlType(FlaUI.Core.Definitions.ControlType controlType)
        {
            return FindInAllWindows(cf => cf.ByControlType(controlType));
        }

        // ---- Interaction Helpers ----
        protected void EnterText(string automationId, string text)
        {
            CheckDisposed();
            var element = FindElementById(automationId);
            if (element == null) return;

            if (element.Patterns.Value.IsSupported)
            {
                try
                {
                    // Clear then set
                    element.Patterns.Value.Pattern.SetValue(string.Empty);
                    element.Patterns.Value.Pattern.SetValue(text);
                    return;
                }
                catch { /* fallback below */ }
            }
            var asTextBox = element.AsTextBox();
            if (asTextBox != null)
            {
                asTextBox.Text = string.Empty;
                asTextBox.Enter(text);
                return;
            }
            // Fallback for controls like PasswordBox
            try
            {
                element.Focus();
                Keyboard.Type(text);
            }
            catch { }
        }

        protected void ClickElement(string automationId)
        {
            CheckDisposed();
            var element = FindElementById(automationId);
            if (element == null) return;
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
                return;
            }
            element.AsButton()?.Click();
        }

        protected void ClickElementByText(string text)
        {
            CheckDisposed();
            var element = FindElementByText(text);
            if (element == null || element.IsOffscreen) return;
            if (element.Patterns.Invoke.IsSupported)
            {
                element.Patterns.Invoke.Pattern.Invoke();
            }
            else
            {
                element.Click();
            }
        }

        protected void ClickHyperlinkByText(string linkText)
        {
            // same as ClickElementByText but separated for semantic clarity
            ClickElementByText(linkText);
        }

        protected string GetElementText(string automationId)
        {
            CheckDisposed();
            var element = FindElementById(automationId);
            if (element == null) return string.Empty;
            if (element.Patterns.Value.IsSupported)
            {
                try { return element.Patterns.Value.Pattern.Value; } catch { }
            }
            return element.AsTextBox()?.Text ?? string.Empty;
        }

        protected bool IsElementVisible(string automationId)
        {
            var element = FindElementById(automationId);
            return element != null && !element.IsOffscreen;
        }

        // ---- Wait Helpers ----
        protected static AutomationElement WaitForElement(Func<AutomationElement> findElement, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                var el = findElement();
                if (el != null && !el.IsOffscreen) return el;
                System.Threading.Tasks.Task.Delay(80).Wait();
            }
            return null;
        }

        protected static bool WaitForCondition(Func<bool> condition, TimeSpan timeout)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                if (condition()) return true;
                System.Threading.Tasks.Task.Delay(80).Wait();
            }
            return false;
        }

        protected void WaitForElementToBeClickable(string automationId, TimeSpan timeout)
        {
            WaitForCondition(() =>
            {
                var el = FindElementById(automationId);
                return el != null && !el.IsOffscreen && el.IsEnabled;
            }, timeout);
        }

        protected static void WaitForElementToDisappear(Func<AutomationElement> findElement, TimeSpan timeout)
        {
            WaitForCondition(() =>
            {
                var el = findElement();
                return el == null || el.IsOffscreen;
            }, timeout);
        }

        // ---- Navigation Helpers ----
        protected void NavigateToSignUp()
        {
            ClickHyperlinkByText("signUpText");
            WaitForElement(() => FindElementById("firstNameTextBox"), TimeSpan.FromSeconds(8));
        }

        protected void NavigateToForgotPassword()
        {
            ClickHyperlinkByText("forgotUserPasswordText");
            // Reuse email textbox (could belong to dialog) - simple wait
            WaitForElement(() => FindElementById("emailTextBox"), TimeSpan.FromSeconds(8));
        }

        protected void NavigateBackToLogin()
        {
            ClickHyperlinkByText("logInText");
            WaitForElement(() => FindElementById("loginButton"), TimeSpan.FromSeconds(8));
        }

        // ---- Safe wrappers ----
        protected void SafeClick(string automationId, TimeSpan? timeout = null)
        {
            var to = timeout ?? TimeSpan.FromSeconds(5);
            WaitForElementToBeClickable(automationId, to);
            ClickElement(automationId);
        }

        protected void SafeEnterText(string automationId, string text, TimeSpan? timeout = null)
        {
            var to = timeout ?? TimeSpan.FromSeconds(5);
            var el = WaitForElement(() => FindElementById(automationId), to);
            if (el == null) return;
            if (el.Patterns.Value.IsSupported)
            {
                try { el.Patterns.Value.Pattern.SetValue(text); return; } catch { }
            }
            var asText = el.AsTextBox();
            if (asText != null)
            {
                asText.Text = string.Empty;
                asText.Enter(text);
                return;
            }
            try { el.Focus(); Keyboard.Type(text); } catch { }
        }
    }
}
