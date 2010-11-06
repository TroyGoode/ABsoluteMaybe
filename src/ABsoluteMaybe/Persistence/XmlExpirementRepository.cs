﻿using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using ABsoluteMaybe.Domain;

namespace ABsoluteMaybe.Persistence
{
	public class XmlExpirementRepository : IExpirementRepository
	{
		private readonly string _pathToXmlStorage;

		public XmlExpirementRepository(string pathToXmlStorage)
		{
			_pathToXmlStorage = pathToXmlStorage;
		}

		protected virtual DateTime UtcNow
		{
			get { return DateTime.UtcNow; }
		}

		#region IExpirementRepository Members

		public void CreateExpirement(string expirementName)
		{
			CreateExpirement(expirementName, expirementName);
		}

		public void CreateExpirement(string expirementName, string conversionKeyword)
		{
			var xml = Load();

			if (xml.Root.Elements("Expirement").Any(x => x.Attribute("Name").Value == expirementName))
				return;

			var exp = new XElement("Expirement",
			                       new XAttribute("Name", expirementName),
			                       new XAttribute("Started", UtcNow)
				);
			if(expirementName != conversionKeyword)
				exp.Add(new XAttribute("ConversionKeyword", conversionKeyword));
			xml.Root.Add(exp);

			Save(xml);
		}

		public ParticipationRecord GetOrCreateParticipationRecord(string expirementName,
		                                                          Func<string> chooseAnOptionForUser,
		                                                          string userId)
		{
			var xml = Load();

			var expirement = xml.Root.Elements("Expirement").Single(x => x.Attribute("Name").Value == expirementName);
			if (expirement.Element("Participants") == null)
				expirement.Add(new XElement("Participants"));

			var participants = expirement.Element("Participants");
			var existingRecord = participants.Elements("Participant").SingleOrDefault(x => x.Attribute("Id").Value == userId);
			if (existingRecord != null)
				return new ParticipationRecord
				       	{
				       		ExpirementName = expirementName,
				       		UserIdentifier = existingRecord.Attribute("Id").Value,
				       		AssignedOption = existingRecord.Value,
				       		HasConverted = false
				       	};

			var assignedOption = chooseAnOptionForUser();
			expirement.Element("Participants").Add(new XElement("Participant",
			                                                    new XAttribute("Id", userId),
			                                                    new XCData(assignedOption)));

			Save(xml);
			return new ParticipationRecord
			       	{
			       		ExpirementName = expirementName,
			       		UserIdentifier = userId,
			       		AssignedOption = assignedOption,
			       		HasConverted = false
			       	};
		}

		public void Convert(string conversionKeyword,
		                    string userId)
		{
			var xml = Load();

			var utcNow = UtcNow;
			var expirements = xml.Root.Elements("Expirement")
				.Where(x =>
				       x.Attribute("Name").Value == conversionKeyword ||
				       (x.Attribute("ConversionKeyword") != null && x.Attribute("ConversionKeyword").Value == conversionKeyword));
			foreach (var participant in expirements
				.Select(expirement => expirement.Element("Participants"))
				.Select(participants => participants.Elements("Participant").Single(x => x.Attribute("Id").Value == userId))
				.Where(participant => participant.Attribute("HasConverted") == null))
			{
				participant.Add(new XAttribute("HasConverted", true));
				participant.Add(new XAttribute("DateConverted", utcNow));
			}
			Save(xml);
		}

		#endregion

		protected virtual XDocument Load()
		{
			return File.Exists(_pathToXmlStorage)
			       	? XDocument.Load(_pathToXmlStorage)
			       	: new XDocument(new XElement("Expirements"));
		}

		protected virtual void Save(XDocument xml)
		{
			xml.Save(_pathToXmlStorage);
		}
	}
}