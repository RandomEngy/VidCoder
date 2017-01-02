using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;
using VidCoder.Services.Windows;

namespace VidCoder.ViewModel
{
	public class CustomMessageDialogViewModel<T>
	{
		private IWindowManager windowManager = Ioc.Get<IWindowManager>();

		public CustomMessageDialogViewModel(string title, string message, IEnumerable<CustomDialogButton<T>> buttons)
		{
			this.Title = title;
			this.Message = message;
			this.Buttons = buttons.ToList();

			foreach (var button in this.Buttons)
			{
				button.Parent = this;
			}
		}

		public string Title { get; }

		public string Message { get; }

		public List<CustomDialogButton<T>> Buttons { get; }

		public T Result { get; private set; }

		public void SetResult(T result)
		{
			this.Result = result;
			this.windowManager.Close(this);
		}
	}

	public class CustomDialogButton<T>
	{
		public CustomDialogButton(T value, string display, ButtonType type = ButtonType.Normal)
		{
			this.Value = value;
			this.Display = display;

			this.IsDefault = type == ButtonType.Default;
			this.IsCancel = type == ButtonType.Cancel;

			this.Choose = ReactiveCommand.Create();
			this.Choose.Subscribe(_ => this.ChooseImpl());
		}

		public T Value { get; }

		public string Display { get; }

		public bool IsDefault { get; }

		public bool IsCancel { get; }

		public CustomMessageDialogViewModel<T> Parent { get; set; }

		public ReactiveCommand<object> Choose { get; }
		private void ChooseImpl()
		{
			this.Parent.SetResult(this.Value);
		}
	}

	public enum ButtonType
	{
		Normal,
		Default,
		Cancel
	}
}
