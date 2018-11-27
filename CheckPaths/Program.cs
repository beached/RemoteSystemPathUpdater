using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace CheckPaths {
	class Program {
		const string EnvironmentPath = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

		static void Main( string[] args ) {
			if( args.Length < 2 ) {
				Console.WriteLine( @"Please supply the path file and then a computer name" );
				return;
			}
			var pathFile = args[0]?.Trim( );
			var computerName = args[1]?.Trim( );
			var minimumPaths = PathsFromFile( pathFile );
			var currentPath = GetRemoteSystemPath( computerName );
			var currentPaths = currentPath.Split( ';' ).ToList( );
			var newPathItems = GetNewPaths( currentPaths, minimumPaths );
			if( !string.IsNullOrEmpty( newPathItems ) ) {
				AddSystemPathItems( computerName, currentPath, newPathItems );
			}
		}

		private static List<string> PathsFromFile( string fileName ) {
			if( !File.Exists( fileName ) ) {
				throw new Exception( $@"Could not locate path file '{fileName}'" );
			}
			var result = new List<string>( );
			using( var fs = File.OpenText( fileName ) ) {
				var currentLine = fs.ReadLine( )?.Trim( );
				while( !(currentLine is null) ) {
					if( string.IsNullOrEmpty( currentLine ) || currentLine.StartsWith( @"#" ) ) {
						// Skip blank lines
						currentLine = fs.ReadLine( )?.Trim( );
						continue;
					}
					result.Add( currentLine );
					currentLine = fs.ReadLine( )?.Trim( );
				}
			}
			return result;
		}

		private static string GetRemoteSystemPath( string computerName ) {
			if( string.IsNullOrEmpty(computerName)) throw new ArgumentNullException(nameof(computerName));

			var result = ReadRegistryValue( computerName, RegistryHive.LocalMachine, EnvironmentPath, @"Path" );
			switch( result ) {
			case null:
				throw new Exception( @"Error connecting to remote registry for reading" );
			case string s:
				return s;
			default:
				throw new Exception( @"Unknown registry value type reading" );
			}
		}

		private static string GetNewPaths( List<string> currentPaths, List<string> minimumPaths ) {
			if (currentPaths is null) throw new ArgumentNullException(nameof(currentPaths));
			if( minimumPaths is null ) throw new ArgumentNullException(nameof(minimumPaths));

			var result = new List<string>( );
			foreach( var p in minimumPaths ) {
				if( currentPaths.FindIndex( x => x.ToUpperInvariant( ).Contains( p.ToUpperInvariant( ) ) ) < 0 ) {
					result.Add( p );
					Console.WriteLine( $@"Missing: {p}" );
				}
			}
			return string.Join( @";", result );
		}

		private static RegistryKey OpenBaseKey( string computerName, RegistryHive rh ) {
			if( string.IsNullOrEmpty( computerName ) ) throw new ArgumentNullException( nameof( computerName ) );

			try {
				if( computerName == @"." || computerName.ToLowerInvariant( ) == @"localhost" ) {
					return RegistryKey.OpenBaseKey( rh, RegistryView.Default );
				}
				return RegistryKey.OpenRemoteBaseKey( rh, computerName );
			} catch( Exception ex ) {
				Console.Error.WriteLine( $"Error connecting to host for registry access\n{ex.Message}\n" );
				return null;
			}
		}

		private static object ReadRegistryValue( string computerName, RegistryHive rh, string path, string valueName ) {
			if( string.IsNullOrEmpty( computerName ) ) throw new ArgumentNullException( nameof( computerName ) );
			if( string.IsNullOrEmpty( path ) ) throw new ArgumentNullException( nameof( path ) );
			if( string.IsNullOrEmpty( valueName ) ) throw new ArgumentNullException( nameof( valueName ) );

			try {
				return OpenBaseKey( computerName, rh )?.OpenSubKey( path )?.GetValue( valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames );
			} catch( Exception ex ) {
				Console.Error.WriteLine( $"Error reading registry\n{ex.Message}\n" );
				return null;
			}
		}

		private static void WriteExpandStringRegistryValue( string computerName, RegistryHive rh, string path, string valueName, string value ) {
			if( string.IsNullOrEmpty( computerName ) ) throw new ArgumentNullException( nameof( computerName ) );
			if( string.IsNullOrEmpty( path ) ) throw new ArgumentNullException( nameof( path ) );
			if( string.IsNullOrEmpty( valueName ) ) throw new ArgumentNullException( nameof( valueName ) );
			if( string.IsNullOrEmpty( value ) ) throw new ArgumentNullException( nameof( value ) );

			OpenBaseKey( computerName, rh )?.OpenSubKey( path, true )?.SetValue( valueName, value, RegistryValueKind.ExpandString );
		}

		private static void AddSystemPathItems( string computerName, string currentPath, string newPathItems ) {
			if( string.IsNullOrEmpty( computerName ) ) throw new ArgumentNullException( nameof( computerName ) );
			if( string.IsNullOrEmpty( currentPath ) ) throw new ArgumentNullException( nameof( currentPath ) );
			if( string.IsNullOrEmpty( newPathItems ) ) throw new ArgumentNullException( nameof( newPathItems ) );

			var newPath = newPathItems + ";" + currentPath;
			WriteExpandStringRegistryValue( computerName, RegistryHive.LocalMachine, EnvironmentPath, @"Path", newPath );
		}
	}
}
