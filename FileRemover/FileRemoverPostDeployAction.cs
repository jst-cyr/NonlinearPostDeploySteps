using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

using HedgehogDevelopment.SitecoreProject.PackageInstallPostProcessor.Contracts;


namespace Nonlinear.PostDeploySteps.FileRemover
{
	/// <summary>
	/// TDS Post-deploy step ensures that after a deployment a configured list of files are removed from the file system.
	/// This allows teams to ensure that old views, config files, and XML overrides can easily be cleaned up from existing systems without having to delete the entire installation.
	/// *NOTE*: File paths in the parameters should be pipe-delimited.
	/// </summary>
	[Description("Removes files from the Website folder.\nFile paths are specified in the Parameter as a pipe (|) separated list.")]
	public class FileRemoverPostDeployAction : IPostDeployAction
	{
		public void RunPostDeployAction(System.Xml.Linq.XDocument deployedItems, IPostDeployActionHost host, string parameter)
		{
			//If an invalid parameter value has been provided, stop processing
			if (String.IsNullOrWhiteSpace(parameter))
			{
				host.LogMessage("[FileRemover] Invalid parameter provided: '{0}'. Skipping post-deploy actions.", parameter);
				return;
			}

			host.LogMessage("[FileRemover] Starting FileRemover deploy action with parameter value: '{0}'", parameter);

			//Extract the list of files, stripping out empty file paths
			var filePaths = parameter.Split('|').Where(filePath => !String.IsNullOrWhiteSpace(filePath)).ToList();

			//Create the name of the folder we want to use for this run.
			var tempFolderName = DateTime.Now.ToString("yyyyMMddHHmmss");

			//Remove each file in the list
			foreach (var filePath in filePaths)
			{
				//Skip files that do not exist
				if (!FileExists(filePath))
				{
					host.LogMessage("[FileRemover] Could not find a file with path '{0}'. Skipping processing of file path.", filePath);
					continue;
				}

				host.LogMessage("[FileRemover] A file with path '{0}' has been found. Ensuring temp directory '{1}' prior to move.", filePath, tempFolderName);

				//Ensure a holding directory exists. We only want to create this once.
				DirectoryInfo tempDir = EnsureTempDirectory(host, tempFolderName);

				//If the temporary directory could not be created, we will not be able to do any file moves so we need to stop all processing
				if (tempDir == null)
				{
					host.LogMessage("[FileRemover] The temporary directory '{0}' is not available. Unable to proceed with file removals without a temp folder. Check security permissions to ensure the application has appropriate permissions to create directories in the local application temp folder.", tempFolderName);
					break;
				}

				//Move the file
				host.LogMessage("[FileRemover] Moving file: '{0}' to temp directory '{1}'.", filePath, tempDir.FullName);
				MoveFile(host, filePath, tempDir);
			}
		}

		/// <summary>
		/// Ensures that a temp directory exists to hold removed files
		/// </summary>
		/// <param name="host">The deploy action host</param>
		/// <param name="folderName">The folder to create</param>
		protected virtual DirectoryInfo EnsureTempDirectory(IPostDeployActionHost host, string folderName)
		{
			//Ensure valid folder name
			if (String.IsNullOrWhiteSpace(folderName))
			{
				host.LogMessage("[FileRemover] Invalid folder name provided: '{0}'. Aborting EnsureTempDirectory.");
				throw new ArgumentException("Empty folder specified. Aborting creation of temp directory.", "folderName");
			}

			DirectoryInfo tempFolder = null;

			try
			{
				//Ensure the parent directory exists
				var appPath = AppDomain.CurrentDomain.BaseDirectory;
				var parentPath = String.Format("{0}/temp/Keystone", appPath);
				DirectoryInfo parentDirectory = Directory.CreateDirectory(parentPath);
				DirectoryInfo subDirectory = parentDirectory.CreateSubdirectory("FileRemover");

				//Ensure the directory with this folder path exists
				tempFolder = subDirectory.CreateSubdirectory(folderName);
			}
			catch (Exception ex)
			{
				host.LogMessage("[FileRemover] Exception occurred while creating temp directory: {0}. Aborting temp folder creation. Message: {1}", folderName, ex.Message);
				tempFolder = null;
			}

			return tempFolder;
		}

		/// <summary>
		/// Check if the relative file path exists
		/// </summary>
		/// <param name="filePath">The file path that is being removed</param>
		/// <returns>If the file exists</returns>
		protected virtual bool FileExists(string filePath)
		{
			var absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);

			return File.Exists(absolutePath);
		}

		/// <summary>
		/// Moves the file specified to the temp directory
		/// </summary>
		/// <param name="host">The deploy action host</param>
		/// <param name="filePath">The file to move</param>
		/// <param name="tempDirectory">The temporary directory to store files in</param>
		protected virtual void MoveFile(IPostDeployActionHost host, string filePath, DirectoryInfo tempDirectory)
		{
			//Get the absolute path
			var absolutePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, filePath);
			var targetPath = Path.Combine(tempDirectory.FullName, filePath);

			//Ensure the folder structure is replicated
			var targetDirectory = Path.GetDirectoryName(targetPath);
			Directory.CreateDirectory(targetDirectory);

			//Attempt to move the file to the temporary directory. Failures must not throw an exception to allow processing of the rest of the files after this one.
			try
			{
				File.Move(absolutePath, targetPath);
			}
			catch (Exception ex)
			{
				host.LogMessage("[FileRemover] An error occurred while moving file '{0}' to location '{1}'. Error Message: {2}", absolutePath, targetPath, ex.Message);
			}
		}
	}
}
