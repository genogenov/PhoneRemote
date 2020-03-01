using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using PhoneRemote.AndroidClient.Client;
using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Net.Sockets;
using System.Threading;
using Xamarin.Essentials;

namespace PhoneRemote.AndroidClient
{
	[Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
	public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener, View.IOnTouchListener
	{
		private int x;
		private int y;
		private MotionEventActions lastTouch;

		private TextView actionText;
		private TextView xText;
		private TextView yText;

		private LinearLayout connectivityBar;
		private TextView serverText;
		private TextView connectionText;

		private ClientFacade client;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			Xamarin.Essentials.Platform.Init(this, savedInstanceState);
			SetContentView(Resource.Layout.activity_main);
			Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
			SetSupportActionBar(toolbar);

			//FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
			//fab.Click += FabOnClick;

			this.actionText = FindViewById<TextView>(Resource.Id.actionText);
			this.xText = FindViewById<TextView>(Resource.Id.xText);
			this.yText = FindViewById<TextView>(Resource.Id.yText);

			this.connectivityBar = FindViewById<LinearLayout>(Resource.Id.connectivityBar);
			this.serverText = FindViewById<TextView>(Resource.Id.serverText);
			this.connectionText = FindViewById<TextView>(Resource.Id.connectionText);

			var mouseController = FindViewById<RelativeLayout>(Resource.Id.mouse_controller);
			DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

			mouseController.SetOnTouchListener(this);
			mouseController.Clickable = true;
			mouseController.Focusable = true;

			ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
			drawer.AddDrawerListener(toggle);
			toggle.SyncState();

			NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
			navigationView.SetNavigationItemSelectedListener(this);

			this.client = new ClientFacade();

			this.client.ServerDiscovery += Client_ServerDiscovery;
			this.client.ConnectionStateChange += Client_ConnectionStateChange;
			this.client.DiscoverAndConnect();
		}

		private void Client_ConnectionStateChange(object sender, PhoneRemoteClientEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				bool isConnected = e.IsConnected;
				this.connectionText.SetText(isConnected ? "Connected" : "Disconnected", TextView.BufferType.Normal);

				var color = isConnected ? Android.Graphics.Color.Green : Android.Graphics.Color.OrangeRed;
				this.connectivityBar.SetBackgroundColor(color);
			});
		}

		private void Client_ServerDiscovery(object sender, PhoneRemoteClientEventArgs e)
		{
			MainThread.BeginInvokeOnMainThread(() =>
			{
				this.serverText.Text = $"{e.Message.ServerName} ({e.Message.Address.ToString()}:{e.Message.Port})";
			});
		}

		private void HandleMouseClick()
		{
			this.client.Send(new CursorAction() { ActionFlags = (uint)(CursorAction.MouseEventFlags.LEFTDOWN | CursorAction.MouseEventFlags.LEFTUP)});
		}

		private void HandleMouseRightClick()
		{
			this.client.Send(new CursorAction() { ActionFlags = (uint)(CursorAction.MouseEventFlags.RIGHTDOWN | CursorAction.MouseEventFlags.RIGHTUP) });
		}

		public override void OnBackPressed()
		{
			DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
			if (drawer.IsDrawerOpen(GravityCompat.Start))
			{
				drawer.CloseDrawer(GravityCompat.Start);
			}
			else
			{
				base.OnBackPressed();
			}
		}

		public override bool OnCreateOptionsMenu(IMenu menu)
		{
			MenuInflater.Inflate(Resource.Menu.menu_main, menu);
			return true;
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			int id = item.ItemId;
			if (id == Resource.Id.action_settings)
			{
				return true;
			}

			return base.OnOptionsItemSelected(item);
		}

		private void FabOnClick(object sender, EventArgs eventArgs)
		{
			View view = (View)sender;
			Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
				.SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
		}

		public bool OnNavigationItemSelected(IMenuItem item)
		{
			int id = item.ItemId;

			if (id == Resource.Id.nav_camera)
			{
				// Handle the camera action
			}
			else if (id == Resource.Id.nav_gallery)
			{

			}
			else if (id == Resource.Id.nav_slideshow)
			{

			}
			else if (id == Resource.Id.nav_manage)
			{

			}
			else if (id == Resource.Id.nav_share)
			{

			}
			else if (id == Resource.Id.nav_send)
			{

			}

			DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
			drawer.CloseDrawer(GravityCompat.Start);
			return true;
		}
		public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
		{
			Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

			base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
		}

		public bool OnTouch(View v, MotionEvent e)
		{
			var newAction = e.Action;

			switch (e.Action)
			{
				case MotionEventActions.Up:
					if(this.lastTouch == MotionEventActions.Down)
					{
						this.HandleMouseClick();
					}
					break;
				case MotionEventActions.Down:
					this.x = (int)e.GetX();
					this.y = (int)e.GetY();
					break;
				case MotionEventActions.Pointer1Up:
					if(this.lastTouch == MotionEventActions.Pointer2Down)
					{
						this.HandleMouseRightClick();
					}
					break;
				case MotionEventActions.Move:
					float x = e.GetX();
					float y = e.GetY();
					int newX = (int)x;
					int newY = (int)y;

					int dx = newX - this.x;
					int dY = newY - this.y;

					newAction = this.lastTouch;

					if (this.lastTouch == MotionEventActions.Pointer2Down)
					{
						break;
					}

					if (Math.Abs(dx) > 3 || Math.Abs(dY) > 3)
					{
						this.x = newX;
						this.y = newY;

						this.client.Send(new CursorAction() { ActionFlags = (uint)CursorAction.MouseEventFlags.MOVE, DX = dx, DY = dY });
						newAction = e.Action;
					}
					break;
			}

			this.lastTouch = newAction;
			this.actionText.Text = this.lastTouch.ToString();
			this.xText.Text = this.x.ToString();
			this.yText.Text = this.y.ToString();

			return true;
		}
	}
}

