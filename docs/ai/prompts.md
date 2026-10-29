# Prompts and Instructions

The main prompts I gave the AI assistant, in order. They are lightly cleaned up for spelling; the conversation also included many short confirmations ("done", "build succeeded", "next") between steps.

## 1. Understanding the task

> *(Attached the exercise PDF.)* It's been a while since I've created everything from backend, data model and UI. Let's build it one by one. Also tell me what they're actually asking me to build here.

> Is this a whole application that consists of a front end, back end, and database?

> What is the best practice here: create the front end first, the back end first, or the database first?

## 2. Working style

> Let's create it one by one, in baby steps: from Git, what to open in the editor, what commands to run, step by step.

> You're giving too many steps at a time. Just give me one by one. Let's take it slow and steady and do it together.

## 3. Environment setup

> *(Pasted the output of `git --version`, `dotnet --list-sdks` and `node --version`.)*

> I already have Visual Studio on my system. *(This turned out to be VS Code, so we used VS Code for both backend and frontend.)*

> There's an old project open in VS Code from six months ago. How do I close it completely?

## 4. Building the backend

> Is it PowerShell or the terminal in VS Code?

> In step 49 you're asking me to create ImportService.cs. Didn't we just create it in step 48 with the same name? *(Clarified: `IImportService` is the interface and `ImportService` is the implementation.)*

> I don't see a Controllers folder. *(It had been removed together with the template's sample controller; recreated.)*

## 5. Pace

> Can we wrap this up faster? We don't have to go as deep. *(We moved to bigger steps with short explanations, and the assistant built the Angular client separately.)*

## 6. Verification and debugging

> *(Pasted the import results and the import-run history table.)* Mouse price is still $24.99 in the file. *(This showed the update test had not been performed, not that there was a bug.)*

> *(Pasted a 404 from PowerShell when fetching the audit history.)* *(Cause: Windows PowerShell passes a JSON array down the pipeline as a single object; fixed by assigning it to a variable first.)*

> *(Pasted `npm start` output stuck on the Angular analytics prompt.)*

## 7. Code style

> When I copy and paste this code, won't it look too AI-generated, or is it fine?

*(Outcome: keep AI use transparent, disclose it as the exercise requires, keep comments focused on* why*, make sure I can explain every part, and commit in small steps.)*

## 8. Documentation

> Write the README and the AI disclosure files.
