namespace MultiplayerDemo.Client.Ui {
	/// <summary>Formatting shared by the HUD and covered by the EditMode tests.</summary>
	public static class ArenaFormat {
		/// <summary>The always-visible match clock, `HH:MM:SS`, from the server's elapsed milliseconds.</summary>
		public static string Elapsed(long totalMilliseconds) {
			if ( totalMilliseconds < 0 ) {
				totalMilliseconds = 0;
			}
			var totalSeconds = totalMilliseconds / 1000L;
			var hours = totalSeconds / 3600L;
			var minutes = (totalSeconds / 60L) % 60L;
			var seconds = totalSeconds % 60L;
			return $"{hours:00}:{minutes:00}:{seconds:00}";
		}

		/// <summary>Milliseconds as `1.2 s`, used for the fire cooldown and the respawn countdown.</summary>
		public static string Seconds(int milliseconds) {
			if ( milliseconds < 0 ) {
				milliseconds = 0;
			}
			return (milliseconds / 1000f).ToString("0.0") + " s";
		}
	}
}
