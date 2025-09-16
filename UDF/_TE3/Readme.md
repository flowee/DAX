# C# script for Tabular Editor 3

<img width="686" height="673" alt="image" src="https://github.com/user-attachments/assets/68fd752b-f4d8-47e8-ad13-5225d70a2f20" />

## Features – TE3 GitHub UDF Manager

 Features:

 ### Organized Tree View of the GitHub Repository

  All *.dax files (DAX UDF functions) are grouped by folder, based on their GitHub path.
  
  You can expand or collapse folders and select one or multiple functions.

  Bold font → The function already exists in your current model.
  
  Regular font → The function exists only in the GitHub repository.

### Compare Model Functions with GitHub

  Click the "Compare" button to check whether functions in your model match their GitHub versions.
  
  Green font → Code is identical between model and GitHub.
  
  Red font → Code differs between model and GitHub.

### Load or Update DAX Functions in Your Model

  Browse and select the desired DAX UDFs from the GitHub repository.
  
  Click the "Update in the Model" button to load new functions, or update existing functions in the model with the latest GitHub version.

### Push Changes to GitHub

  Click the "Update in GitHub" button to update .dax files in the GitHub repository using the function definitions from your model.
  
  This action only updates existing functions — it does not create new .dax files.

### Authentication
 
   GitHub Token Required.

  Set your GitHub token as an environment variable: GITHUB_TOKEN (User or Machine level).
  
  Required for accessing both public and private repositories.
