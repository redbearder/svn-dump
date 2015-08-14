﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using FinalSoftware.MySql;
using MySql = FinalSoftware.MySql.MySqlWrapper;
using System.Text.RegularExpressions;

namespace eA_Script_VarParser {

	public class ScriptScanner {
		private static MySql mSql;
		private static frmMain mForm;

		public static void Initialize( frmMain Form ) {
			mForm = Form;

			ConnectionHolder con = new ConnectionHolder( "localhost", 3306, "eathena_test", "eathena_test", "eathena_test" );
			mSql = new FinalSoftware.MySql.MySqlWrapper();
			if( mSql.Init( con ) != MysqlError.None )
				throw mSql.LastError;
		}

		public static void ScanDir( string Dirpath ) {
			if( Dirpath.EndsWith( "\\" ) == false )
				Dirpath += "\\";
			string[] files = Directory.GetFiles( Dirpath, "*.txt" );
			string[] dirs = Directory.GetDirectories( Dirpath );

			for( int i = 0; i < files.Length; i++ )
				ScanFile( files[ i ] );

			for( int i = 0; i < dirs.Length; i++ )
				if( dirs[ i ].EndsWith( ".svn" ) == false )
					ScanDir( dirs[ i ] );
		}


		public static void ScanFile( string Filepath ) {
			mForm.AddLog( "scaning " + Filepath );

			List<string> Lines = new List<string>( File.ReadAllLines( Filepath ) );
			List<ParserLine> npcLines = new List<ParserLine>();
			ScriptParserList scriptParser = new ScriptParserList();
			bool mMultiComment = false;
			Match m;
			string npcName = "";
			int codeOpen = 0;

			for( int i = 0; i < Lines.Count; i++ ) {
				string line = Lines[ i ].Trim();

				#region Comment removal
				// empty or comment
				if( line.Length == 0 || line.StartsWith( "//" ) == true )
					continue;

				// multiline continue
				if( mMultiComment == true ) {
					if( line.Contains( "*/" ) == false )
						continue;

					// */ found, end it
					mMultiComment = false;
					if( ( line = line.Substring( line.IndexOf( "*/" ) ) ).Length == 0 )
						continue;
				}

				// start of multiline
				if( line.StartsWith( "/*" ) == true ) {
					// only this line?
					if( line.EndsWith( "*/" ) == true )
						continue;

					// rly multiline
					if( line.Contains( "*/" ) == false ) {
						mMultiComment = true;
						continue;
					}

					// line contains */, get the substr
					if( ( line = line.Substring( line.IndexOf( "*/" ) + 2 ) ).Length == 0 )
						continue;
				}

				#endregion

				// okay, need to check for a new Script with Strings
				if( codeOpen == 0 && ( m = Regex.Match( line, "^[^\t]+\t[^\t]+\t([^\t]+)\t[^{]*{", RegexOptions.IgnoreCase ) ).Success == true ) {
					// start of a new Script
					codeOpen++;
					npcName = m.Groups[ 1 ].Captures[ 0 ].Value;
					if( npcName.IndexOf( "::" ) != -1 )
						npcName = npcName.Substring( npcName.IndexOf( "::" ) + 2 );
					npcLines.Clear();
					continue;
				} else if( codeOpen > 0 ) {
					// we are in a Script block
					codeOpen += line.CountChar( '{' );
					codeOpen -= line.CountChar( '}' );
					if( codeOpen > 0 ) {
						// still in context, add to npcLines
						npcLines.Add( new ParserLine( i, line ) );
						continue;
					}

					// last bracket is closed, NPC is complete!
					if( npcLines.Count == 0 )
						continue; // huh, no valid line found? o.o

					// start Parser with the Script Lines
					ScriptParser parser = new ScriptParser();
					parser.Lines = npcLines.ToArray();
					parser.Scriptpath = Filepath;
					parser.NpcName = npcName;
					parser.Parse();
					if( parser.Messages.Count == 0 )
						continue; // no Messages to work with, next Script!

					scriptParser.Add( parser ); // stor parser to replace Lines after finishing
					FillDB( parser );
				}
				// else: no Script block, no valid Script head, nothing todo here

			}

			// nothing to replace
			//if( scriptParser.Count == 0 )
			return;

			foreach( ScriptParser parser in scriptParser ) {
				foreach( ScriptMessage msg in parser.Messages.Values ) {
					Lines[ msg.Index ] = Lines[ msg.Index ].Replace( "\"" + msg.Message + "\"", "GetLanguageString( " + msg.Constant + " )" );
					Lines[ msg.Index ] += " // " + msg.Message;
				}
				foreach( KeyValuePair<int, ScriptMessage> pair in parser.Duplicates ) {
					ScriptMessage msg = pair.Value;
					Lines[ pair.Key ] = Lines[ pair.Key ].Replace( "\"" + msg.Message + "\"", "GetLanguageString( " + msg.Constant + " )" );
					Lines[ pair.Key ] += " // " + msg.Message;
				}
			}

			// add Constants to end of Script
			List<string> consts = scriptParser.GetConstants();
			Lines.AddRange( new string[] { "", "", "" } ); // 3 lines
			Lines.Add( "/* auto generated by GodLesZ' Script Parser 1.0.2" );
			Lines.Add( "\t#Language constants used#" );
			for( int i = 0; i < consts.Count; i++ )
				Lines.Add( "\t\t" + consts[ i ] );
			Lines.Add( "*/" );

			// write new File
			File.Delete( Filepath );
			File.WriteAllLines( Filepath, Lines.ToArray() );
		}


		private static void FillDB( ScriptParser parser ) {
			string dbPath = parser.Scriptpath;
			int pos = dbPath.IndexOf( "npc\\" );
			if( pos != -1 )
				dbPath = dbPath.Substring( pos + 4 );
			dbPath = dbPath.Replace( ".txt", "" );

			foreach( ScriptMessage mes in parser.Messages.Values ) {
				int aff = mSql.QuerySimple( "REPLACE INTO lang_english VALUES ( '{0}', '{1}', '{2}', '{3}' );", dbPath.MysqlEscape(), parser.NpcName.MysqlEscape(), mes.Constant.MysqlEscape(), mes.Message.MysqlEscape() );
				if( mSql.LastError != null )
					throw mSql.LastError;
			}

		}

	}

}