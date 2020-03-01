using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using PhoneRemote.Core;
using PhoneRemote.Protobuf;
using PhoneRemote.Protobuf.ProtoModels;
using System;
using System.Net.Sockets;
using System.Threading;

namespace PhoneRemote.AndroidClient
{
	[Activity(Label = "@string/app_name", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
	public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener, View.IOnTouchListener
	{
		private int x;
		private int y;
		private string lastTouch;

		private TextView actionText;
		private TextView xText;
		private TextView yText;
		private ILogger<PhoneRemoteClient<IMessage>> logger;

		private PhoneRemoteClient<IMessage> tcpClient;
		private bool isConnected = false;

		protected override void OnCreate(Bundle savedInstanceState)
		{
			this.logger = LoggerFactory.Create(x => x.ClearProviders()).CreateLogger<PhoneRemoteClient<IMessage>>();
			this.tcpClient = new PhoneRemoteClient<IMessage>(new ProtobufMessageSerializer(), this.logger);

			tcpClient.DiscoverServerAsync<ServiceDiscoveryMessage>(CancellationToken.None).ContinueWith(res =>
			{
				if (res.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
				{
					tcpClient.ConnectAsync(new System.Net.IPEndPoint(res.Result.IpAddress, res.Result.Port), CancellationToken.None).ContinueWith(t =>
					{
						if (res.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
						{
							isConnected = true;
						}
					});
				}
			});

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

			var mouseController = FindViewById<RelativeLayout>(Resource.Id.mouse_controller);
			DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

			mouseController.SetOnTouchListener(this);

			ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
			drawer.AddDrawerListener(toggle);
			toggle.SyncState();

			NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
			navigationView.SetNavigationItemSelectedListener(this);
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
			float x = e.GetX();
			float y = e.GetY();
			this.lastTouch = e.Action.ToString();

			bool shouldSend = true;
			switch (e.Action)
			{
				case MotionEventActions.Down:
					this.x = (int)x;
					this.y = (int)y;
					break;
				case MotionEventActions.Move:
					int newX = (int)x;
					int newY = (int)y;
					int dx = newX - this.x;
					int dY = newY - this.y;

					this.x = newX;
					this.y = newY;

					if (this.isConnected && shouldSend)
					{
						this.tcpClient.SendAsync(new CursorPosition() { DX = dx, DY = dY }, CancellationToken.None);
					}
					break;
				case MotionEventActions.Up:
					this.x = (int)x;
					this.y = (int)y;
					break;
			}
			this.actionText.Text = this.lastTouch;
			this.xText.Text = this.x.ToString();
			this.yText.Text = this.y.ToString();

			return true;
		}
	}
}

