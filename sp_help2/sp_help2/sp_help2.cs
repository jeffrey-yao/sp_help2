using System;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.IO;
using Microsoft.SqlServer.Server;
using System.Collections.Generic;
using System.Text;

public partial class StoredProcedures
{
	[Microsoft.SqlServer.Server.SqlProcedure(Name ="sp_help2")]
	public static void sp_help2 (string Name, string ShowType )
	{
		string definition;
		string database;


		using (SqlConnection conn = new SqlConnection("context connection=true"))
		{
			//DataSet ds = new DataSet();
			DataTable dt = new DataTable();
			dt.Columns.Add(new DataColumn("Item", System.Type.GetType("System.String")));
			dt.Columns.Add(new DataColumn("Description", System.Type.GetType("System.String")));

			SqlCommand qry = new SqlCommand();

			if (ShowType.ToUpper() != "T" && ShowType.ToUpper() != "S")
			{
				throw new System.ArgumentException("Invalid parameter value, valid values are T=Table Format, S=String Format", "ShowType");
			}

			SqlParameter nameParam = new SqlParameter("@Name", SqlDbType.NVarChar);
		 
			qry.Parameters.Add(nameParam);


			nameParam.Value = Name;
			
			qry.Connection = conn;
			qry.CommandText = @"
								declare @db varchar(128);
								select @db = db_name(resource_database_id) 
									from sys.dm_tran_locks 
									where request_session_id = @@spid  
									and resource_type = 'database' 
									and request_owner_type = 'SHARED_TRANSACTION_WORKSPACE';
								select dbname=isnull(@db, db_name());
								";
			
			conn.Open();
			database = (string)qry.ExecuteScalar();

			qry.CommandText = "use [" + database + "]; \r\n";
			qry.CommandText += "select isnull(definition, '') from sys.sql_modules where object_id=object_id(@Name);";

			definition = (String) qry.ExecuteScalar();

			if (string.IsNullOrEmpty(definition) )
			{
				definition = string.Format("[{0}] not found in database [{1}] ", Name, database);
			}
			else
			{
				Dictionary<string, string> dict = new Dictionary<string, string>();
				// initialize the first record
				dict.Add("Name", Name + "\r\n");
				

				string patt = @"(?s)/\*.+?\*/";
				Regex rex = new Regex(patt);
				Match m = rex.Match(definition);
				// follwing is the key word list, and can be modified to meet your own requirement 
				patt = @"(?i)^\s*\.(function|parameter|example|CreatedBy|CreatedOn|ModifiedBy|Modification|Note):?$"; 
				rex = new Regex(patt);
				string key = string.Empty;
				string value = m.Value.Replace("/*", "");
				value = value.Replace("*/", "");

				if (!string.IsNullOrEmpty(value))
				{
					using (StringReader reader = new StringReader(value))
					{
						string line = string.Empty;
						while ((line = reader.ReadLine()) != null)
						{
							line = line.Trim();
							if (string.IsNullOrEmpty(line))
							{
								continue;
							}

							if (line.Length >= 2 && line.Substring(0, 2) == "--") //check if the line is commented out
							{
								continue;
							}
							if (rex.IsMatch(line))
							{
								var mt = rex.Match(line);
								key = mt.Groups[1].Value;
								if (!dict.ContainsKey(key))
								{ dict.Add(key, ""); }
							  
							}
							else
							{
								if (!String.IsNullOrEmpty(key))
								{
									dict[key] += line + Environment.NewLine;
								   
								}
							}

						}
					}
					if (dict.Count > 0)
					{
						StringBuilder sb = new StringBuilder("");
						

						// populate definition with dict values
						foreach(var v in dict)
						{
							sb.AppendLine(v.Key);
							sb.AppendLine(v.Value);
							//sb.AppendLine("");
							DataRow dr = dt.NewRow();
							dr["Item"] = v.Key;
							dr["Description"] = v.Value;
							dt.Rows.Add(dr);
						}
					   
						definition = sb.ToString();
					}
				}
			}

			conn.Close();

			if (ShowType == "S")
			{ SqlContext.Pipe.Send(definition); }
			else
			{
				SqlDataRecord record = new SqlDataRecord(
				new SqlMetaData("item", SqlDbType.VarChar, 4000),
				new SqlMetaData("description", SqlDbType.VarChar, 4000));
				SqlPipe pipe = SqlContext.Pipe;
				pipe.SendResultsStart(record);
				try
				{
					foreach (DataRow row in dt.Rows)
					{
						for (int index = 0; index < record.FieldCount; index++)
						{
							string value = row[index].ToString();
							record.SetValue(index, value);
						}

						pipe.SendResultsRow(record);
					}
				}
				finally
				{
					pipe.SendResultsEnd();
				}
			}
			


		}
		

	}

}
