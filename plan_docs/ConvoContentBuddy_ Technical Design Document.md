# **ConvoContentBuddy: Technical Design Document**

## **1\. System Architecture**

ConvoContentBuddy operates as an autonomous background listener. The architecture is split into four primary layers:

### **A. Audio Input & Transcription Layer**

* **Mechanism:** Uses the browser's Web Speech API (specifically SpeechRecognition).  
* **Flow:** Captures continuous audio streams, converting them into timestamped text segments. It filters out silence and manages "active" conversation buffers.

### **B. Context & Intent Analysis Layer (The "Brain")**

* **Model:** Powered by gemini-2.5-flash-preview-09-2025.  
* **Process:** Periodically (or upon detecting specific keywords like "array", "pointer", "sum") the system sends the recent transcript buffer to Gemini.  
* **Goal:** Identify the specific LeetCode problem title or number and the programming language being discussed.

### **C. Resource Retrieval Layer**

* **Mechanism:** Google Search Grounding via Gemini API.  
* **Flow:** Once a problem is identified, the system automatically triggers a search for:  
  * Optimal time/space complexity solutions.  
  * Step-by-step logic explanations.  
  * Code implementations in multiple languages (Python, Java, C++).

### **D. Ambient UI Layer**

* **Design Philosophy:** Non-intrusive, dashboard-style display.  
* **Components:**  
  * **Live Feed:** Real-time transcript visualization.  
  * **Active Problem Card:** Highlighting the detected challenge.  
  * **Solution Panel:** Code snippets and complexity analysis that update as the conversation evolves.

## **2\. LeetCode Implementation Details**

* **Trigger Phrases:** The system listens for common patterns like "Given an array of integers," "Find the maximum," or "Two sum."  
* **Search Optimization:** Gemini is prompted to find "official" and "top-rated community" solutions (e.g., from NeetCode or LeetCode Discuss) to ensure high-quality support.

## **3\. Autonomous Feedback Loop**

1. **Listen:** User speaks about "merging two sorted lists."  
2. **Detect:** Gemini identifies "LeetCode 21: Merge Two Sorted Lists."  
3. **Fetch:** System searches and retrieves the Python recursive and iterative solutions.  
4. **Display:** The UI slides in the solution before the interviewer even finishes the prompt.