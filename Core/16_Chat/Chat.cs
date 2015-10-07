//
// Copyright (c) 2008-2015 the Urho3D project.
// Copyright (c) 2015 Xamarin Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System.Collections.Generic;
using System.Text;

namespace Urho.Samples
{
	public class _16_Chat : Sample
	{
		// Identifier for the chat network messages
		const int MsgChat = 32;
		// UDP port we will use
		const short ChatServerPort = 2345;

		/// Strings printed so far.
		List<string> chatHistory = new List<string>();
		/// Chat text element.
		Text chatHistoryText;
		/// Button container element.
		UIElement buttonContainer;
		/// Server address / chat message line editor element.
		LineEdit textEdit;
		/// Send button.
		Button sendButton;
		/// Connect button.
		Button connectButton;
		/// Disconnect button.
		Button disconnectButton;
		/// Start server button.
		Button startServerButton;


		public _16_Chat(Context ctx) : base(ctx) { }

		public override void Start()
		{
			base.Start();
			Input.SetMouseVisible(true, false);
			CreateUI();
			SubscribeToEvents();
		}

		void CreateUI()
		{
			IsLogoVisible = false; // We need the full rendering window

			var graphics = Graphics;
			UIElement root = UI.Root;
			var cache = ResourceCache;
			XMLFile uiStyle = cache.GetXmlFile("UI/DefaultStyle.xml");
			// Set style to the UI root so that elements will inherit it
			root.SetDefaultStyle(uiStyle);

			Font font = cache.GetFont("Fonts/Anonymous Pro.ttf");
			chatHistoryText = new Text(Context);
			chatHistoryText.SetFont(font, 12);
			root.AddChild(chatHistoryText);

			buttonContainer = new UIElement(Context);
			root.AddChild(buttonContainer);
			buttonContainer.SetFixedSize(graphics.Width, 20);
			buttonContainer.SetPosition(0, graphics.Height - 20);
			buttonContainer.LayoutMode=LayoutMode.LM_HORIZONTAL;

			textEdit = new LineEdit(Context); 
			textEdit.SetStyleAuto(null);
			buttonContainer.AddChild(textEdit);

			sendButton = CreateButton("Send", 70);
			connectButton = CreateButton("Connect", 90);
			disconnectButton = CreateButton("Disconnect", 100);
			startServerButton = CreateButton("Start Server", 110);

			UpdateButtons();

			// No viewports or scene is defined. However, the default zone's fog color controls the fill color
			Renderer.DefaultZone.FogColor = new Color(0.0f, 0.0f, 0.1f);
		}

		void SubscribeToEvents()
		{
			SubscribeToTextFinished(args => HandleSend());
			SubscribeToReleased(args =>
				{
					if (args.Element == sendButton)
						HandleSend();
					if (args.Element == connectButton)
						HandleConnect();
					if (args.Element == disconnectButton)
						HandleDisconnect();
					if (args.Element == startServerButton)
						HandleStartServer();
				});
			SubscribeToLogMessage(HandleLogMessage);
			SubscribeToNetworkMessage(HandleNetworkMessage);
			SubscribeToServerConnected(args => UpdateButtons());
			SubscribeToServerDisconnected(args => UpdateButtons());
			SubscribeToConnectFailed(args => UpdateButtons());
		}
	
		Button CreateButton(string text, int width)
		{
			var cache = ResourceCache;
			Font font = cache.GetFont("Fonts/Anonymous Pro.ttf");

			Button button = new Button(Context);
			buttonContainer.AddChild(button);
			button.SetStyleAuto(null);
			button.SetFixedWidth(width);
	
			var buttonText = new Text(Context);
			button.AddChild(buttonText);
			buttonText.SetFont(font, 12);
			buttonText.SetAlignment(HorizontalAlignment.HA_CENTER, VerticalAlignment.VA_CENTER);

			buttonText.Value = text;
	
			return button;
		}

		void ShowChatText(string row)
		{
			chatHistory.Add(row);
			chatHistoryText.Value = string.Join("\n", chatHistory) + "\n";
		}

		void UpdateButtons()
		{
			Network network = Network;
			Connection serverConnection = network.ServerConnection;
			bool serverRunning = network.IsServerRunning();
	
			// Show and hide buttons so that eg. Connect and Disconnect are never shown at the same time
			sendButton.SetVisible(serverConnection != null);
			connectButton.SetVisible(serverConnection == null && !serverRunning);
			disconnectButton.SetVisible(serverConnection != null || serverRunning);
			startServerButton.SetVisible(serverConnection == null && !serverRunning);
		}

		void HandleLogMessage(LogMessageEventArgs args)
		{
			ShowChatText(args.Message);
		}

		void HandleSend()
		{
			string text = textEdit.Text;
			if (string.IsNullOrEmpty(text))
				return; // Do not send an empty message
	
			Network network = Network;
			Connection serverConnection = network.ServerConnection;
	
			if (serverConnection != null)
			{
				// Send the chat message as in-order and reliable
				serverConnection.SendMessage(MsgChat, true, true, Encoding.UTF8.GetBytes(text));
				// Empty the text edit after sending
				textEdit.Text = string.Empty;
			}
		}

		void HandleConnect()
		{
			Network network = Network;
			string address = textEdit.Text.Trim();
			if (string.IsNullOrEmpty(address))
				address = "localhost"; // Use localhost to connect if nothing else specified
			// Empty the text edit after reading the address to connect to
			textEdit.Text= string.Empty;

			// Connect to server, do not specify a client scene as we are not using scene replication, just messages.
			// At connect time we could also send identity parameters (such as username) in a VariantMap, but in this
			// case we skip it for simplicity
			network.Connect(address, ChatServerPort, null);
	
			UpdateButtons();
		}

		void HandleDisconnect()
		{
			Network network = Network;
			Connection serverConnection = network.ServerConnection;
			// If we were connected to server, disconnect
			if (serverConnection != null)
				serverConnection.Disconnect(0);
			// Or if we were running a server, stop it
			else if (network.IsServerRunning())
				network.StopServer();
	
			UpdateButtons();
		}

		void HandleStartServer()
		{
			Network network = Network;
			network.StartServer((ushort)ChatServerPort);
	
			UpdateButtons();
		}

		unsafe void HandleNetworkMessage(NetworkMessageEventArgs args)
		{
			Network network = Network;
	
			int msgID = args.MessageID;
			if (msgID == MsgChat)
			{
				var textBytes = args.Data;
				var text = Encoding.UTF8.GetString(textBytes, 0, textBytes.Length);
			
				// If we are the server, prepend the sender's IP address and port and echo to everyone
				// If we are a client, just display the message
				if (network.IsServerRunning())
				{
					Connection sender = args.Connection;
					text = sender + " " + text;
					// Broadcast as in-order and reliable
					fixed (byte* p = textBytes)
						network.BroadcastMessage(MsgChat, true, true, p, (uint) textBytes.Length, 0);
				}

				ShowChatText(text);
			}
		}

		protected override string JoystickLayoutPatch => JoystickLayoutPatches.Hidden;
	}
}
