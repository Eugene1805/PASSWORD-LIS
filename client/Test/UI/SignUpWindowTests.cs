using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.UIA3;
using System;
using System.Threading;

namespace Test.UI
{
    public class SignUpWindowTests : IDisposable
    {
        private Application app;
        private UIA3Automation automation;
        private Window mainWindow;

        public SignUpWindowTests()
        {
            // Iniciar la aplicación
            app = Application.Launch(@"C:\Users\eugen\Documents\Tecnologías\PASSWORD LIS\client\PASSWORD LIS Client\bin\Debug\PASSWORD LIS Client.exe");
            automation = new UIA3Automation();
            mainWindow = app.GetMainWindow(automation);

            // Esperar a que la ventana esté lista
            WaitForElement(() => FindElementById("signUpButton"), 5000);
        }

        public void Dispose()
        {
            automation?.Dispose();
            app?.Close();
        }

        // Métodos helper
        protected void EnterText(string automationId, string text)
        {
            var textBox = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox();
            textBox?.Enter(text);
        }

        protected void EnterPassword(string automationId, string password)
        {
            // Para PasswordBox, usamos el mismo método ya que FlaUI maneja ambos
            var passwordBox = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsTextBox();
            passwordBox?.Enter(password);
        }

        protected void ClickButton(string automationId)
        {
            var button = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.AsButton();
            button?.Click();
        }

        protected string GetText(string automationId)
        {
            var element = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            return element?.AsTextBox()?.Text ?? string.Empty;
        }

        protected bool IsElementVisible(string automationId)
        {
            var element = mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            return element != null && element.IsOffscreen;
        }
        protected AutomationElement FindElementById(string automationId)
        {
            return mainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
        }
        protected AutomationElement WaitForElement(Func<AutomationElement> findElement, int timeout = 3000)
        {
            var retryCount = timeout / 100;
            for (int i = 0; i < retryCount; i++)
            {
                var element = findElement();
                if (element != null && !element.IsOffscreen)
                    return element;

                Thread.Sleep(100);
            }
            return null;
        }
    }
}
